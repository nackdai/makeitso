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
        }

        private void parseFileCudaCompile(XmlNode item, MakeItSoConfig_Project projConfig)
        {
            var inc = item.Attributes["Include"];
            if (inc == null)
            {
                return;
            }

            CudaCompileInfo info = new CudaCompileInfo();

            info.m_file = inc.Value;

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
                        opt.m_AdditionalOptions = child.InnerText;
                        opt.m_AdditionalOptions = opt.m_AdditionalOptions.Replace("%(AdditionalOptions)", "");
                    }
                }

                child = child.NextSibling;
            }

            m_compileInfos.Add(info);
        }

        private void parseCommonCudaCompile(XmlNode item, MakeItSoConfig_Project projConfig)
        {
            var parent = item.ParentNode;

            var cond = parent.Attributes["Condition"].Value;
            var configuration = parseCondition(cond);

            foreach (var info in m_compileInfos)
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

        private void parseCudaCompileOptions(CudaCompileOption opt, string configuration, XmlNode item, MakeItSoConfig_Project projConfig)
        {        
            if (item.Name == "GenerateRelocatableDeviceCode")
            {
                opt.m_GenerateRelocatableDeviceCode = checkIsTrue(item.InnerText);
            }
            else if (item.Name == "TargetMachinePlatform")
            {
                opt.m_TargetMachinePlatform = Int32.Parse(item.InnerText);
            }
            else if (item.Name == "CodeGeneration")
            {
                opt.m_CodeGeneration = item.InnerText;
            }
            else if (item.Name == "NvccCompilation")
            {
                opt.m_NvccCompilation = item.InnerText;
            }
            else if (item.Name == "FastMath")
            {
                opt.m_FastMath = checkIsTrue(item.InnerText);
            }
            else if (item.Name == "CudaRuntime")
            {
                opt.m_CudaRuntime = item.InnerText;
            }
            else if (item.Name == "GPUDebugInfo")
            {
                opt.m_GPUDebugInfo = checkIsTrue(item.InnerText);
            }
            else if (item.Name == "HostDebugInfo")
            {
                opt.m_GenerateHostDebugInfo = checkIsTrue(item.InnerText);
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

        public class CudaCompileOption
        {
            public string GenerateRelocatableDeviceCode
            {
                get
                {
                    return m_GenerateRelocatableDeviceCode ? "-rdc=true" : "";
                }
            }

            public string AdditionalOptions
            {
                get
                {
                    return m_AdditionalOptions;
                }
            }

            public string NvccCompilation
            {
                get
                {
                    if (m_NvccCompilation == "compile")
                    {
                        return "--compile";
                    }

                    // TODO
                    // Throw exception...
                    return "";
                }
            }

            public string CudaRuntime
            {
                get
                {
                    if (m_CudaRuntime == "Static")
                    {
                        return "-cudart static";
                    }

                    // TODO
                    // Throw exception...
                    return "";
                }
            }

            public string TargetMachinePlatform
            {
                get
                {
                    string ret = "--machine " + m_TargetMachinePlatform.ToString();
                    return ret;
                }
            }

            public string GPUDebugInfo
            {
                get
                {
                    return m_GPUDebugInfo ? "-G" : "";
                }
            }

            public string FastMath
            {
                get
                {
                    return m_FastMath ? "--use_fast_math" : "";
                }
            }

            public string GenerateHostDebugInfo
            {
                get
                {
                    return m_GenerateHostDebugInfo ? "-g" : "";
                }
            }

            public string CodeGeneration
            {
                get
                {
                    return m_CodeGeneration;
                }
            }

            internal void SetValueByConfiguration(string configuration)
            {
                var config = configuration.ToLower();

                if (config.Contains("debug"))
                {
                    m_GPUDebugInfo = true;
                    m_GenerateHostDebugInfo = true;
                }
                else
                {
                    m_GPUDebugInfo = false;
                    m_GenerateHostDebugInfo = false;
                }
            }

            internal bool m_GenerateRelocatableDeviceCode = false;
            internal string m_NvccCompilation = "compile";
            internal string m_CudaRuntime = "Static";
            internal int m_TargetMachinePlatform = 64;
            internal bool m_GPUDebugInfo = false;
            internal bool m_FastMath = false;
            internal bool m_GenerateHostDebugInfo = false;
            internal string m_CodeGeneration = "compute_20,sm_20";
            internal string m_AdditionalOptions = "";
        }

        public class CudaCompileInfo
        {
            public CudaCompileOption getOption(string configuration)
            {
                if (!m_options.ContainsKey(configuration))
                {
                    m_options.Add(configuration, new CudaCompileOption());
                    m_options[configuration].SetValueByConfiguration(configuration);
                }

                return m_options[configuration];
            }

            public string File
            {
                get
                {
                    return m_file;
                }
            }

            internal string m_file;
            private Dictionary<string, CudaCompileOption> m_options = new Dictionary<string, CudaCompileOption>();
        }

        public List<CudaCompileInfo> CompileInfos
        {
            get
            {
                return m_compileInfos;
            }
        }

        private List<CudaCompileInfo> m_compileInfos = new List<CudaCompileInfo>();
    }
}
