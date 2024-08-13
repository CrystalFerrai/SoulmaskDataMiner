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

using System.Diagnostics;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Utility to assist with debugging
	/// </summary>
	internal static class DebugUtil
	{
		/// <summary>
		/// Waits for a debugger to attach, if one is not already attached
		/// </summary>
		public static void WaitForDebugger()
		{
			if (Debugger.IsAttached) return;

			Console.Out.WriteLine("Waiting for debugger... Press any key to skip.");
			while (!Debugger.IsAttached)
			{
				if (Console.KeyAvailable)
				{
					Console.ReadKey(true);
					break;
				}
				Thread.Sleep(100);
			}

			Debugger.Break();
		}
	}
}
