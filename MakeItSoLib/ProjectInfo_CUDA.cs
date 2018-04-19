using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Xml;

namespace MakeItSoLib
{
    public class ProjectInfo_CUDA
    {
        public class CudaCompileOption
        {
            public void SetValueByConfiguration(string configuration)
            {
                var config = configuration.ToLower();

                if (config.Contains("debug"))
                {
                    GPUDebugInfo = true;
                    GenerateHostDebugInfo = true;
                }
                else
                {
                    GPUDebugInfo = false;
                    GenerateHostDebugInfo = false;
                }
            }

            public bool GenerateRelocatableDeviceCode { get; set; } = false;

            public string NvccCompilation { get; set; } = "compile";

            public string CudaRuntime { get; set; } = "Static";

            public int TargetMachinePlatform { get; set; } = 64;

            public bool GPUDebugInfo { get; set; } = false;

            public bool FastMath { get; set; } = false;

            public bool GenerateHostDebugInfo { get; set; } = false;

            public string CodeGeneration { get; set; } = "compute_20,sm_20";

            public string AdditionalOptions { get; set; } = "";
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

                set
                {
                    m_file = value;
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

        public void addCompileInfo(CudaCompileInfo info)
        {
            m_compileInfos.Add(info);
        }

        private List<CudaCompileInfo> m_compileInfos = new List<CudaCompileInfo>();
    }
}
