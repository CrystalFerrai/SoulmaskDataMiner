﻿// Copyright 2024 Crystal Ferrai
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

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// General configuration information the program needs to run
	/// </summary>
	internal class Config
	{
		/// <summary>
		/// The location of the "Soulmask/Content" directory within a Soulmask installation
		/// </summary>
		public string GameContentDirectory { get; set; }

		/// <summary>
		/// The directory to write all output files
		/// </summary>
		public string OutputDirectory { get; set; }

		/// <summary>
		/// The encryption key to use when accessing pak files
		/// </summary>
		public string? EncryptionKey { get; set; }

		/// <summary>
		/// A list of miners to run
		/// </summary>
		public IReadOnlyList<string>? Miners { get; set; }

		private Config()
		{
			GameContentDirectory = null!;
			OutputDirectory = null!;
			EncryptionKey = null;
			Miners = null;
		}

		public static bool TryParseCommandLine(string[] args, Logger logger, [NotNullWhen(true)] out Config? result)
		{
			if (args.Length == 0)
			{
				result = null;
				return false;
			}

			Config instance = new();

			int positionalArgIndex = 0;

			for (int i = 0; i < args.Length; ++i)
			{
				if (args[i].StartsWith("--"))
				{
					// Explicit arg
					string argValue = args[i][2..];
					switch (argValue)
					{
						case "key":
							if (i < args.Length - 1 && !args[i + 1].StartsWith("--"))
							{
								instance.EncryptionKey = args[i + 1];
								++i;
							}
							else
							{
								logger.LogError("Missing parameter for --key argument");
								result = null;
								return false;
							}
							break;
						case "miners":
							if (i < args.Length - 1 && !args[i + 1].StartsWith("--"))
							{
								MineRunner.ListAllMiners(out List<string> defaultMinerList, out List<string> additionalMinerList);
								HashSet<string> defaultMiners = new(defaultMinerList);
								HashSet<string> additionalMiners = new(additionalMinerList);

								string[] miners = args[i + 1].Split(',').Select(m => m.Trim()).ToArray();
								List<string> unknownMiners = new();
								foreach (string miner in miners)
								{
									if (!defaultMiners.Contains(miner, StringComparer.OrdinalIgnoreCase) &&
										!additionalMiners.Contains(miner, StringComparer.OrdinalIgnoreCase))
									{
										unknownMiners.Add(miner);
									}
								}

								if (unknownMiners.Count > 0)
								{
									logger.Log(LogLevel.Warning, $"The following specified miners were not found: {string.Join(',', unknownMiners)}");
								}

								if (miners.Length == unknownMiners.Count)
								{
									logger.LogError("No specified miners were found.");
									result = null;
									return false;
								}

								instance.Miners = miners;
								++i;
							}
							else
							{
								logger.LogError("Missing parameter for --miners argument");
								result = null;
								return false;
							}
							break;
						default:
							logger.LogError($"Unrecognized argument '{args[i]}'");
							result = null;
							return false;
					}
				}
				else
				{
					// Positional arg
					switch (positionalArgIndex)
					{
						case 0:
							instance.GameContentDirectory = Path.GetFullPath(args[i]);
							break;
						case 1:
							instance.OutputDirectory = Path.GetFullPath(args[i]);
							break;
						default:
							logger.LogError("Too many positional arguments.");
							result = null;
							return false;
					}
					++positionalArgIndex;
				}
			}

			if (positionalArgIndex < 2)
			{
				logger.LogError($"Not enough positional arguments");
				result = null;
				return false;
			}

			if (!Directory.Exists(instance.GameContentDirectory))
			{
				logger.LogError($"The specified game asset directory \"{instance.GameContentDirectory}\" does not exist or is inaccessible");
				result = null;
				return false;
			}

			result = instance;
			return true;
		}

		/// <summary>
		/// Prints how to use the program, including all possible command line arguments
		/// </summary>
		/// <param name="logger">Where the message will be printed</param>
		/// <param name="logLevel">The log level for the message</param>
		/// <param name="indent">Every line of the output will be prefixed with this</param>
		public static void PrintUsage(Logger logger, LogLevel logLevel, string indent = "")
		{
			string programName = Assembly.GetExecutingAssembly().GetName().Name ?? "SoulmaskDataMiner";
			MineRunner.ListAllMiners(out List<string> defaultMiners, out List<string> additionalMiners);

			logger.Log(logLevel, $"{indent}Usage: {programName} [[options]] [game assets directory] [output directory]");
			logger.LogEmptyLine(logLevel);
			logger.Log(logLevel, $"{indent}  [game assets directory]  Path to a directory containing .pak files for a game.");
			logger.LogEmptyLine(logLevel);
			logger.Log(logLevel, $"{indent}  [output directory]       Directory to output exported assets.");
			logger.LogEmptyLine(logLevel);
			logger.Log(logLevel, $"{indent}Options");
			logger.LogEmptyLine(logLevel);
			logger.Log(logLevel, $"{indent}  --key [key]       The AES encryption key for the game's data.");
			logger.LogEmptyLine(logLevel);
			logger.Log(logLevel, $"{indent}  --miners [miners] Comma separated list of miners to run. If not specified,");
			logger.Log(logLevel, $"{indent}                    default miners will run.");
			logger.LogEmptyLine(logLevel);
			logger.Log(logLevel, $"{indent}Avaialable Miners");
			logger.LogEmptyLine(logLevel);
			logger.Log(logLevel, $"{indent}  Default:    {string.Join(',', defaultMiners)}");
			logger.LogEmptyLine(logLevel);
			logger.Log(logLevel, $"{indent}  Additional: {string.Join(',', additionalMiners)}");
		}
	}
}
