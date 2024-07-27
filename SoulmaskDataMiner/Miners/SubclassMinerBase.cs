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

using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Base class for miners that gather information about specific class heirarchies
	/// </summary>
	[RequireHeirarchy(true)]
	internal abstract class SubclassMinerBase : IDataMiner
	{
		public abstract string Name { get; }

		public abstract bool Run(IProviderManager providerManager, Config config, Logger logger, TextWriter sqlWriter);

		/// <summary>
		/// The name of the property that stores the name to associate with the class.
		/// </summary>
		protected abstract string NameProperty { get; }

		/// <summary>
		/// The name of the property that stores the description to associate with the class.
		/// </summary>
		protected virtual string? DescriptionProperty => null;

		/// <summary>
		/// The name of the property that stores the icon to associate with the class.
		/// </summary>
		protected virtual string? IconProperty => null;

		/// <summary>
		/// Gathers a list of classes which derive from a specific class
		/// </summary>
		protected IEnumerable<ObjectInfo> FindObjects(IEnumerable<string> baseClassNames)
		{
			List<ObjectInfo> infos = new();
			foreach (string className in baseClassNames)
			{
				foreach (BlueprintClassInfo classInfo in BlueprintHeirarchy.Get().GetDerivedClasses(className))
				{
					if (classInfo.Export?.ExportObject.Value is UClass classObj)
					{
						ObjectInfo obj = new() { ClassName = classInfo.Name };
						FindObjectProperties(classObj, ref obj);
						infos.Add(obj);
					}
				}
			}
			infos.Sort();

			return infos;
		}

		private void FindObjectProperties(UClass classObj, ref ObjectInfo obj)
		{
			if (classObj.ClassDefaultObject.TryLoad(out UObject? defaults))
			{
				foreach (FPropertyTag property in defaults!.Properties)
				{
					if (obj.Name is null && string.Equals(property.Name.Text, NameProperty, StringComparison.OrdinalIgnoreCase))
					{
						obj.Name = GameUtil.ReadTextProperty(property);
					}
					else if (obj.Description is null && string.Equals(property.Name.Text, DescriptionProperty, StringComparison.OrdinalIgnoreCase))
					{
						obj.Description = GameUtil.ReadTextProperty(property);
					}
					else if (obj.Icon is null && string.Equals(property.Name.Text, IconProperty, StringComparison.OrdinalIgnoreCase))
					{
						obj.Icon = GameUtil.ReadTextureProperty(property);
					}
				}

				if (obj.Name is not null &&
					(obj.Description is not null || DescriptionProperty is null) &&
					(obj.Icon is not null || IconProperty is null))
				{
					// Found everything
					return;
				}

				if (classObj.Super is not null &&
					classObj.Super.TryLoad(out UObject? super) &&
					super is UBlueprintGeneratedClass superBlueprint)
				{
					// Search parents for anything we didn't find
					FindObjectProperties(superBlueprint, ref obj);
				}
			}
		}

		protected struct ObjectInfo : IComparable<ObjectInfo>
		{
			public string ClassName;
			public string? Name;
			public string? Description;
			public UTexture2D? Icon;

			public int CompareTo(ObjectInfo other)
			{
				if (Name is null)
				{
					if (other.Name is not null) return -1;
					return ClassName.CompareTo(other.ClassName);
				}
				int result = Name.CompareTo(other.Name);
				if (result == 0)
				{
					return ClassName.CompareTo(other.ClassName);
				}
				return result;
			}
		}
	}
}
