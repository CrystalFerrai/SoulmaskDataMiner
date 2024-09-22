// Copyright 2024 Crystal Ferrai
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CUE4Parse.Compression;

namespace SoulmaskDataMiner
{
	internal class Program
	{
		private static int Main(string[] args)
		{
			Logger logger = new ConsoleLogger();

			if (args.Length == 0)
			{
				Config.PrintUsage(logger, LogLevel.Important);
				return OnExit(0);
			}

			Config? config;
			if (!Config.TryParseCommandLine(args, logger, out config))
			{
				logger.LogEmptyLine(LogLevel.Information);
				Config.PrintUsage(logger, LogLevel.Important);
				return OnExit(1);
			}

			try
			{
				Directory.CreateDirectory(config.OutputDirectory);
			}
			catch (Exception ex)
			{
				logger.Log(LogLevel.Fatal, $"Could not access/create output directory \"{config.OutputDirectory}\". [{ex.GetType().FullName}] {ex.Message}");
				return OnExit(1);
			}

			ZlibHelper.Initialize(ZlibHelper.DLL_NAME);
			OodleHelper.Initialize(OodleHelper.OODLE_DLL_NAME);

			bool success;
			using (MineRunner runner = new(config, logger))
			{
				if (!runner.Initialize()) return OnExit(1);
				success = runner.Run();
			}

			logger.Log(LogLevel.Important, "Done.");

			if (!success)
			{
				logger.Log(LogLevel.Warning, "\nOne or more miners failed. See above for details.");
			}

			return OnExit(0);
		}

		private static int OnExit(int code)
		{
			if (System.Diagnostics.Debugger.IsAttached)
			{
				Console.Out.WriteLine("Press a key to exit");
				Console.ReadKey(true);
			}
			return code;
		}
	}
}
