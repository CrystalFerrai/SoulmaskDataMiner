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

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Base class for all data mienrs
	/// </summary>
	internal abstract class MinerBase : IDataMiner
	{
		public abstract string Name { get; }

		public abstract bool Run(IProviderManager providerManager, Config config, Logger logger, ISqlWriter sqlWriter);

		/// <summary>
		/// Formats a string so that it is safe to insert into a CSV cell
		/// </summary>
		protected static string? CsvStr(string? value)
		{
			return SqlUtil.CsvStr(value);
		}

		/// <summary>
		/// Formats a string so that it is safe to insert into a SQL database cell
		/// </summary>
		protected static string DbStr(string? value, bool treatNullAsEmpty = false)
		{
			return SqlUtil.DbStr(value, treatNullAsEmpty);
		}

		/// <summary>
		/// Formats a bool to be inserted into a SQL database cell
		/// </summary>
		protected static string DbBool(bool value)
		{
			return SqlUtil.DbBool(value);
		}

		/// <summary>
		/// Formats a nullable value to be inserted into a SQL database cell
		/// </summary>
		protected static string DbVal<T>(Nullable<T> value) where T : struct
		{
			return SqlUtil.DbVal(value);
		}
	}
}
