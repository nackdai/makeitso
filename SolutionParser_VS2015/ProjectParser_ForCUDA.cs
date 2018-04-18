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

            var config = MakeItSoConfig.Instance.getProjectConfig(projectName);

            string inputxml = File.ReadAllText(path);
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(inputxml);

            XmlNamespaceManager xmlnsmgr = new XmlNamespaceManager(xmlDocument.NameTable);
            xmlnsmgr.AddNamespace("rs", "http://schemas.microsoft.com/developer/msbuild/2003");

            XmlNode rootNode = xmlDocument.DocumentElement;

            XmlNodeList cudaItems = rootNode.SelectNodes("/rs:Project/rs:ItemGroup/rs:CudaCompile", xmlnsmgr);

            foreach (XmlNode item in cudaItems)
            {
                parseCudaItems(item, config);
            }

            XmlNodeList commonCudaCompileItems = rootNode.SelectNodes("/rs:Project/rs:ItemDefinitionGroup/rs:CudaCompile", xmlnsmgr);

            foreach (XmlNode item in commonCudaCompileItems)
            {
            }
        }

        private void parseCudaItems(XmlNode item, MakeItSoConfig_Project projConfig)
        {
            var inc = item.Attributes["Include"];
            if (inc == null)
            {
                return;
            }

            CompileInfo info = new CompileInfo();

            info.m_file = inc.Value;

            var child = item.FirstChild;
            
            while (child != null)
            {
                var cond = child.Attributes["Condition"].Value;
                var configuration = parseCondition(cond);

                if (!projConfig.configurationShouldBeRemoved(configuration))
                {
                    if (child.Name == "GenerateRelocatableDeviceCode")
                    {

                    }
                    else if (child.Name == "AdditionalOptions")
                    {
                    }
                }

                child = child.NextSibling;
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

        public class CompileOption
        {
            public bool IsGenerateRelocatableDeviceCode
            {
                get
                {
                    return m_isGenerateRelocatableDeviceCode;
                }
            }

            public string AdditionalOptions
            {
                get
                {
                    return m_additionalOptions;
                }
            }

            internal bool m_isGenerateRelocatableDeviceCode;
            internal string m_additionalOptions;
        }

        public class CompileInfo
        {
            CompileOption getOption(string configuration)
            {
                var ret = m_options[configuration];

                if (ret == null)
                {
                    ret = new CompileOption();
                    m_options.Add(configuration, ret);
                }

                return ret;
            }

            internal string m_file;
            private Dictionary<string, CompileOption> m_options;
        }
    }
}
