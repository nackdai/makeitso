﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MakeItSoLib;

namespace MakeItSo
{
    /// <summary>
    /// Creates a makefile for one C++ project in the solution.
    /// </summary><remarks>
    /// Project makefiles have the name [project-name].makefile. They will
    /// mostly be invoked from the 'master' makefile at the solution root.
    /// 
    /// These makefiles have:
    /// - One main target for each configuration (e.g. debug, release) in the project
    /// - A default target that builds them all
    /// - A 'clean' target
    /// 
    ///   .PHONY: build_all_configurations
    ///   build_all_configurations: Debug Release
    ///   
    ///   .PHONY: Debug
    ///   Debug: debug/main.o debug/math.o debug/utility.o
    ///       g++ debug/main.o debug/math.o debug/utility.o -o output/hello.exe
    ///       
    ///   (And similarly for the Release configuration.)
    ///   
    /// We build the source files once for each configuration. For each one, we also
    /// build a dependency file, which we include if it is available.
    /// 
    ///   -include debug/main.d
    ///   main.o: main.cpp
    ///       g++ -c main.cpp -o debug/main.o
    ///       g++ -MM main.cpp > debug/main.d
    /// 
    /// </remarks>
    class MakefileBuilder_Project_CUDA
    {
        public static void createCudaLocationAndCompiler(StreamWriter writer, string projectName)
        {
            var projConfig = MakeItSoConfig.Instance.getProjectConfig(projectName);
            var cudaVersion = projConfig.getCudaVersion();

            if (cudaVersion != string.Empty || cudaVersion.Length > 0)
            {
                // Cuda location.
                writer.WriteLine("# Location of the CUDA Toolkit");
                var path = "/usr/local/cuda-" + cudaVersion;
                writer.WriteLine("CUDA_PATH ?= \"{0}\"", path);

                // NVCC.
                writer.WriteLine("NVCC:= $(CUDA_PATH)/bin/nvcc -ccbin $(CPP_COMPILER)");

                writer.WriteLine("");
            }
        }

        public static void createFileTargets(
            StreamWriter writer, 
            ProjectInfo_CPP projectInfo, 
            ProjectConfigurationInfo_CPP configurationInfo, 
            ProjectInfo_CUDA projectCudaInfo)
        {
            var includePath = String.Format("$({0})", MakefileBuilder_Project_CPP.getIncludePathVariableName(configurationInfo));
            var preprocessorDefinitions = String.Format("$({0})", MakefileBuilder_Project_CPP.getPreprocessorDefinitionsVariableName(configurationInfo));
            var intermediateFolder = MakefileBuilder_Project_CPP.getIntermediateFolder(projectInfo, configurationInfo);

            var projectSettings = MakeItSoConfig.Instance.getProjectConfig(projectInfo.Name);

            foreach (var info in projectCudaInfo.CompileInfos)
            {
                var filename = info.File;

                if (projectSettings.filesOrDirectoriesShouldBeRemoved(filename))
                {
                    continue;
                }

                var opt = info.getOption(configurationInfo.Name);
                string compileFlags = getCudaCompileFlags(configurationInfo.Name, opt);

                string path = String.Format("{0}/{1}", intermediateFolder, filename);
                if (filename.StartsWith(".."))
                {
                    var tmp = filename.Replace("../", "");
                    path = String.Format("{0}/{1}", intermediateFolder, tmp);
                }
                string objectPath = Path.ChangeExtension(path, ".o");

                writer.WriteLine("# Compiles file {0} for the {1} configuration...", filename, configurationInfo.Name);
                writer.WriteLine("{0}: {1}", objectPath, filename);
                writer.WriteLine("\t$(NVCC) {0} {1} {2} -c {3} -o {4}", compileFlags, includePath, preprocessorDefinitions, filename, objectPath);
                writer.WriteLine("");
            }
        }

        private static string getCudaCompileFlags(string configuration, ProjectInfo_CUDA.CudaCompileOption compileOption)
        {
            string flags = "";

            flags += compileOption.GenerateRelocatableDeviceCode ? " -rdc=true" : "";

            switch (compileOption.NvccCompilation)
            {
                case "compile":
                    flags += " --compile";
                    break;
                default:
                    // TODO
                    break;
            }
            
            switch (compileOption.CudaRuntime)
            {
                case "Static":
                    flags += " -cudart static";
                    break;
                default:
                    break;
            }

            flags += " --machine " + compileOption.TargetMachinePlatform.ToString();

            flags += compileOption.GPUDebugInfo ? " -G" : "";

            flags += compileOption.GenerateHostDebugInfo ? " -g" : "";

            flags += compileOption.FastMath ? " -use_fast_math" : "";

            {
                string[] separator = { "," };
                var strs = compileOption.CodeGeneration.Split(separator, System.StringSplitOptions.RemoveEmptyEntries);

                string sm = "";
                string compute = "";

                sm = strs[0].StartsWith("sm_") ? strs[0] : strs[1];
                compute = strs[0].StartsWith("compute_") ? strs[0] : strs[1];

                flags += string.Format(" -gencode arch={0},code={1}", compute, sm);
            }

            flags += " " + compileOption.AdditionalOptions;

            flags += string.Format(" -Xcompiler ,$({0}_Compiler_Flags)", configuration);

            return flags;
        }
    }
}
