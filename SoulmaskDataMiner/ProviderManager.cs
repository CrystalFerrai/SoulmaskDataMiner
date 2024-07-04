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

using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Default implementation of IProviderManager
	/// </summary>
	internal class ProviderManager : IProviderManager, IDisposable
	{
		private bool mIsDisposed;

		private readonly Config mConfig;

		private readonly DefaultFileProvider mProvider;

		public IFileProvider Provider => mProvider;

		public ProviderManager(Config config)
		{
			mConfig = config;
			mProvider = new DefaultFileProvider(Path.Combine(config.GameContentDirectory, "Paks"), SearchOption.TopDirectoryOnly);
		}

		public bool Initialize(Logger logger)
		{
			InitializeProvider(mProvider);

			return true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~ProviderManager()
		{
			Dispose(false);
		}

		private void Dispose(bool disposing)
		{
			if (!mIsDisposed)
			{
				if (disposing)
				{
					// Dispose managed objects
					mProvider.Dispose();
				}

				// Free unmanaged resources

				mIsDisposed = true;
			}
		}

		private void InitializeProvider(DefaultFileProvider provider)
		{
			provider.Initialize();

			FAesKey key;
			if (mConfig.EncryptionKey is null)
			{
				key = new(new byte[32]);
			}
			else
			{
				key = new(mConfig.EncryptionKey);
			}

			foreach (var vfsReader in provider.UnloadedVfs)
			{
				provider.SubmitKey(vfsReader.EncryptionKeyGuid, key);
			}

			provider.LoadLocalization(ELanguage.English);
		}
	}
}
