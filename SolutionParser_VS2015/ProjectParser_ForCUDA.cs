using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MakeItSoLib;
using System.IO;
using System.Reflection;
using System.Xml;

namespace SolutionParser_VS2010
{
    /// <summary>
    /// Parses a C++ project.
    /// </summary><remarks>
    /// We extract information from a VCProject object, and fill in a  ProjectInfo structure.
    /// </remarks>
    internal class ProjectParser_ForCUDA
    {
        public ProjectParser_ForCUDA(string path, string projectName)
        {
            path = path.ToLower();

            if (File.Exists(path) == false)
            {
                return;
            }

            var projConfig = MakeItSoConfig.Instance.getProjectConfig(projectName);

            string inputxml = File.ReadAllText(path);
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(inputxml);

            XmlNamespaceManager xmlnsmgr = new XmlNamespaceManager(xmlDocument.NameTable);
            xmlnsmgr.AddNamespace("rs", "http://schemas.microsoft.com/developer/msbuild/2003");

            XmlNode rootNode = xmlDocument.DocumentElement;

            XmlNodeList cudaItems = rootNode.SelectNodes("/rs:Project/rs:ItemGroup/rs:CudaCompile", xmlnsmgr);

            foreach (XmlNode item in cudaItems)
            {
                parseFileCudaCompile(item, projConfig);
            }

            XmlNodeList commonCudaCompileItems = rootNode.SelectNodes("/rs:Project/rs:ItemDefinitionGroup/rs:CudaCompile", xmlnsmgr);

            foreach (XmlNode item in commonCudaCompileItems)
            {
                parseCommonCudaCompile(item, projConfig);
            }

            if (m_Project.CompileInfos.Count == 0)
            {
                // NOTE
                //<ImportGroup Label="ExtensionSettings">
                //  <Import Project="$(VCTargetsPath)\BuildCustomizations\CUDA8.0.props"/>
                //</ImportGroup>

                // Check if CUDA project or not.
                XmlNodeList importGroups = rootNode.SelectNodes("/rs:Project/rs:ImportGroup", xmlnsmgr);

                foreach (XmlNode group in importGroups)
                {
                    var attrib = group.Attributes["Label"];
                    if (attrib != null && attrib.Value == "ExtensionSettings")
                    {
                        var item = group.FirstChild;
                        if (item != null)
                        {
                            var prop = item.Attributes["Project"];
                            if (prop != null)
                            {
                                m_Project.IsCUDA = prop.Value.Contains("CUDA");
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                m_Project.IsCUDA = true;
            }
        }

        private void parseFileCudaCompile(XmlNode item, MakeItSoConfig_Project projConfig)
        {
            var inc = item.Attributes["Include"];
            if (inc == null)
            {
                return;
            }

            ProjectInfo_CUDA.CudaCompileInfo info = new ProjectInfo_CUDA.CudaCompileInfo();

            info.File = inc.Value;
            info.File = info.File.Replace("\\", "/");

            var child = item.FirstChild;
            
            while (child != null)
            {
                var cond = child.Attributes["Condition"].Value;
                var configuration = parseCondition(cond);

                if (!projConfig.configurationShouldBeRemoved(configuration))
                {
                    configuration = parseConfiguration(configuration);
                    var opt = info.getOption(configuration);
                    parseCudaCompileOptions(opt, configuration, child, projConfig);

                    // TODO
                    if (child.Name == "AdditionalOptions")
                    {
                        opt.AdditionalOptions = child.InnerText;
                        opt.AdditionalOptions = opt.AdditionalOptions.Replace("%(AdditionalOptions)", "");
                    }
                }

                child = child.NextSibling;
            }

            m_Project.addCompileInfo(info);
        }

        private void parseCommonCudaCompile(XmlNode item, MakeItSoConfig_Project projConfig)
        {
            var parent = item.ParentNode;

            var cond = parent.Attributes["Condition"].Value;
            var configuration = parseCondition(cond);

            if (m_Project.CompileInfos.Count == 0)
            {
                if (!projConfig.configurationShouldBeRemoved(configuration))
                {
                    configuration = parseConfiguration(configuration);
                    var opt = m_Project.AllCompileInfo.getOption(configuration);
                    var node = item.FirstChild;
                    parseCudaCompileOptions(opt, configuration, node, projConfig);
                }
            }
            else
            {
                foreach (var info in m_Project.CompileInfos)
                {
                    var child = item.FirstChild;

                    while (child != null)
                    {
                        if (!projConfig.configurationShouldBeRemoved(configuration))
                        {
                            configuration = parseConfiguration(configuration);
                            var opt = info.getOption(configuration);
                            parseCudaCompileOptions(opt, configuration, child, projConfig);
                        }

                        child = child.NextSibling;
                    }
                }
            }
        }

        private void parseCudaCompileOptions(ProjectInfo_CUDA.CudaCompileOption opt, string configuration, XmlNode item, MakeItSoConfig_Project projConfig)
        {        
            if (item.Name == "GenerateRelocatableDeviceCode")
            {
                opt.GenerateRelocatableDeviceCode = checkIsTrue(item.InnerText);
            }
            else if (item.Name == "TargetMachinePlatform")
            {
                opt.TargetMachinePlatform = Int32.Parse(item.InnerText);
            }
            else if (item.Name == "CodeGeneration")
            {
                opt.CodeGeneration = item.InnerText;
            }
            else if (item.Name == "NvccCompilation")
            {
                opt.NvccCompilation = item.InnerText;
            }
            else if (item.Name == "FastMath")
            {
                opt.FastMath = checkIsTrue(item.InnerText);
            }
            else if (item.Name == "CudaRuntime")
            {
                opt.CudaRuntime = item.InnerText;
            }
            else if (item.Name == "GPUDebugInfo")
            {
                opt.GPUDebugInfo = checkIsTrue(item.InnerText);
            }
            else if (item.Name == "HostDebugInfo")
            {
                opt.GenerateHostDebugInfo = checkIsTrue(item.InnerText);
            }
        }

        static private string parseCondition(string cond)
        {
            string[] separator = { "==" };
            var strs = cond.Split(separator, System.StringSplitOptions.RemoveEmptyEntries);

            if (strs.Length != 2)
            {
                // TODO
                // Throw exception?
                return string.Empty;
            }

            var target = strs[1];
            target = target.Replace("'", "");

            return target;
        }

        static private string parseConfiguration(string config)
        {
            string[] separator = { "|" };
            var strs = config.Split(separator, System.StringSplitOptions.RemoveEmptyEntries);

            return strs[0];
        }

        static private bool checkIsTrue(string val)
        {
            return val == "true";
        }

        public ProjectInfo_CUDA Project
        {
            get
            {
                return m_Project;
            }
        }

        private ProjectInfo_CUDA m_Project = new ProjectInfo_CUDA();
    }
}
