using System;
using System.Collections;
using System.IO;
using System.Text;
using Exortech.NetReflector;
using ThoughtWorks.CruiseControl.Core.Util;
using ThoughtWorks.CruiseControl.Remote;

namespace ThoughtWorks.CruiseControl.Core.Builder
{
	[ReflectorType("nant")]
	public class NAntBuilder : IBuilder
	{
		public const int DEFAULT_BUILD_TIMEOUT = 600;
		public const string DEFAULT_EXECUTABLE = "nant.exe";
		public const string DEFAULT_LABEL = "NO-LABEL";
		public const string DEFAULT_LOGGER = "NAnt.Core.XmlLogger";

		private ProcessExecutor _executor;

		public NAntBuilder() : this(new ProcessExecutor()) { }

		public NAntBuilder(ProcessExecutor executor)
		{
			_executor = executor;
		}

		[ReflectorProperty("executable", Required = false)] 
		public string Executable = DEFAULT_EXECUTABLE;

		[ReflectorProperty("baseDirectory", Required = false)] 
		public string ConfiguredBaseDirectory;

		[ReflectorProperty("buildFile", Required = false)] 
		public string BuildFile;

		[ReflectorProperty("buildArgs", Required = false)] 
		public string BuildArgs;

		[ReflectorProperty("logger", Required = false)]
		public string Logger = DEFAULT_LOGGER;

		[ReflectorArray("targetList", Required = false)] 
		public string[] Targets = new string[0];

		/// <summary>
		/// Gets and sets the maximum number of seconds that the build may take.  If the build process takes longer than
		/// this period, it will be killed.  Specify this value as zero to disable process timeouts.
		/// </summary>
		[ReflectorProperty("buildTimeoutSeconds", Required = false)] 
		public int BuildTimeoutSeconds = DEFAULT_BUILD_TIMEOUT;

		/// <summary>
		/// Runs the integration using NAnt.  The build number is provided for labelling, build
		/// timeouts are enforced.  The specified targets are used for the specified NAnt build file.
		/// StdOut from nant.exe is redirected and stored.
		/// </summary>
		/// <param name="result">For storing build output.</param>
		public void Run(IIntegrationResult result)
		{
			ProcessResult processResult = AttemptExecute(CreateProcessInfo(result));
			result.Output = processResult.StandardOutput;

			if (processResult.TimedOut)
			{
				throw new BuilderException(this, "NAnt process timed out (after " + BuildTimeoutSeconds + " seconds)");
			}

			if (processResult.ExitCode == 0)
			{
				result.Status = IntegrationStatus.Success;
			}
			else
			{
				result.Status = IntegrationStatus.Failure;
				Log.Info("NAnt build failed: " + processResult.StandardError);
			}
		}

		private ProcessInfo CreateProcessInfo(IIntegrationResult result)
		{
			ProcessInfo info = new ProcessInfo(Executable, CreateArgs(result), BaseDirectory(result));
			info.TimeOut = BuildTimeoutSeconds*1000;
			return info;
		}

		private string BaseDirectory(IIntegrationResult result)
		{
			if (ConfiguredBaseDirectory == null || ConfiguredBaseDirectory == "")
			{
				return result.WorkingDirectory;
			}
			else if (Path.IsPathRooted(ConfiguredBaseDirectory))
			{
				return ConfiguredBaseDirectory;
			}
			else
			{
				return Path.Combine(result.WorkingDirectory, ConfiguredBaseDirectory);
			}
		}

		protected ProcessResult AttemptExecute(ProcessInfo info)
		{
			try
			{
				return _executor.Execute(info);
			}			
			catch (Exception e)
			{
				throw new BuilderException(this, string.Format("Unable to execute: {0}\n{1}", BuildCommand, e), e);
			}
		}

		private string BuildCommand
		{
			get { return string.Format("{0} {1}", Executable, BuildArgs); }
		}

		/// <summary>
		/// Creates the command line arguments to the nant.exe executable. These arguments
		/// specify the build-file name, the targets to build to, 
		/// </summary>
		/// <returns></returns>
		internal string CreateArgs(IIntegrationResult result)
		{
			return string.Format("{0} {1} {2} {3} {4}", CreateBuildFileArg(), CreateLoggerArg(), BuildArgs, CreateLabelToApplyArg(result), string.Join(" ", Targets)).Trim();
		}

		private string CreateBuildFileArg()
		{
			if (StringUtil.IsBlank(BuildFile)) return string.Empty;

			return "-buildfile:" + BuildFile;
		}

		private string CreateLoggerArg()
		{
			if (StringUtil.IsBlank(Logger)) return string.Empty;

			return "-logger:" + Logger;
		}

		private string CreateLabelToApplyArg(IIntegrationResult result)
		{
			string label = StringUtil.IsBlank(result.Label) ? DEFAULT_LABEL : result.Label;
			return "-D:label-to-apply=" + label;
		}

		public bool ShouldRun(IIntegrationResult result)
		{
			return result.Working && result.HasModifications();
		}

		public override string ToString()
		{
			string baseDirectory = (ConfiguredBaseDirectory != null ? ConfiguredBaseDirectory : "");
			return string.Format(@" BaseDirectory: {0}, Targets: {1}, Executable: {2}, BuildFile: {3}", baseDirectory, string.Join(", ", Targets), Executable, BuildFile);
		}

		public string TargetsForPresentation
		{
			get
			{
				StringBuilder combined = new StringBuilder();
				bool isFirst = true;
				foreach (string file in Targets)
				{
					if (! isFirst)
					{
						combined.Append(Environment.NewLine);
					}
					combined.Append(file);
					isFirst = false;
				}
				return combined.ToString();
			}
			set
			{
				if (value == null || value == string.Empty)
				{
					Targets = new string[0];
					return;
				}
				ArrayList files = new ArrayList();
				using (StringReader reader = new StringReader(value))
				{
					while(true)
					{
						string line = reader.ReadLine();
						if(line != null)
						{
							files.Add(line);
						}
						else
						{
							break;
						}
					}
				}
				Targets = (string[]) files.ToArray(typeof (string));
			}
		}
	}
}