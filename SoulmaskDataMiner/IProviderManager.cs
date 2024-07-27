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

using CUE4Parse.FileProvider;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Manages asset providers and shared utilities
	/// </summary>
	internal interface IProviderManager
	{
		/// <summary>
		/// Gets the provider associated with the game's content directory
		/// </summary>
		IFileProvider Provider { get; }

		/// <summary>
		/// Metadata for game classes, if it was passed to the program. Mapped by class name.
		/// </summary>
		/// <remarks>
		/// A miner which requires this data should declare the attribute [RequireClassData(true)]. This will
		/// cause the miner to be skipped if the data is not available, thus it is safe to assume the data
		/// exists during the run of the miner. A miner that can optionally use the data should first null
		/// check the property to ensure it exists.
		/// </remarks>
		IReadOnlyDictionary<string, MetaClass>? ClassMetadata { get; }
	}
}
