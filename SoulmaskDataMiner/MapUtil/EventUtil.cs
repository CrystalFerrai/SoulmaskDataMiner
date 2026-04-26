// Copyright 2026 Crystal Ferrai
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

using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using SoulmaskDataMiner.Data;
using SoulmaskDataMiner.GameData;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace SoulmaskDataMiner.MapUtil
{
	/// <summary>
	/// Map processor for world events
	/// </summary>
	internal static class EventUtil
	{
		public static EventMap BuildEventMap(IReadOnlyList<FObjectExport> eventManagerObjects, Logger logger)
		{
			EventMapImpl eventsMap = new();
			foreach (FObjectExport eventManagerObject in eventManagerObjects)
			{
				ECustomGameMode gameMode = ECustomGameMode.Survival;
				Dictionary<int, string>? eventNameMap = null;

				foreach (FPropertyTag property in eventManagerObject.ExportObject.Value.Properties)
				{
					switch (property.Name.Text)
					{
						case "SpecailEventMap":
							{
								UScriptMap? eventMap = property.Tag?.GetValue<UScriptMap>();
								if (eventMap is null) break;

								eventNameMap = new();
								foreach (var pair in eventMap.Properties)
								{
									FStructFallback? eventStruct = pair.Value?.GetValue<FStructFallback>();
									if (eventStruct is null) continue;

									int eventId = -1;
									string? eventName = null;
									foreach (FPropertyTag eventProperty in eventStruct.Properties)
									{
										switch (eventProperty.Name.Text)
										{
											case "EventID":
												eventId = eventProperty.Tag!.GetValue<int>();
												break;
											case "EventDescribeText":
												eventName = DataUtil.ReadTextProperty(eventProperty);
												break;
										}
									}

									if (eventId < 0 || eventName is null)
									{
										logger.Warning($"Failed to parse event properties for event manager {eventManagerObject.ObjectName}");
										continue;
									}

									eventNameMap.Add(eventId, eventName);
								}
							}
							break;
						case "EventCustomGameMode":
							if (DataUtil.TryParseEnum(property, out ECustomGameMode mode))
							{
								gameMode = mode;
							}
							break;
					}
				}

				if (eventNameMap is null)
				{
					logger.Warning($"Failed to parse event manager {eventManagerObject.ObjectName}");
					continue;
				}

				if (!eventsMap.TryAdd(gameMode, eventNameMap))
				{
					logger.Warning($"Duplicate event manager game mode {gameMode} found");
				}
			}

			return eventsMap;
		}

		public static IReadOnlyDictionary<int, EventData> ProcessEventMap(EventMap map, IReadOnlyDictionary<int, List<SpawnData>> eventSpawnMap, string mapName, Logger logger)
		{
			Dictionary<int, EventData> eventMap = new();

			foreach (var modePair in map.OrderBy(p => p.Key))
			{
				foreach (var eventPair in modePair.Value.OrderBy(p => p.Key))
				{
					EventData eventData;
					if (!eventMap.TryGetValue(eventPair.Key, out eventData))
					{
						eventData = new()
						{
							Id = eventPair.Key,
							Names = new()
						};
					}
					eventData.Names.Add(eventPair.Value);
					eventData.ModeMask |= modePair.Key.CreateMask();
					if (eventSpawnMap.TryGetValue(eventPair.Key, out List<SpawnData>? spawnData))
					{
						eventData.SpawnData = spawnData;
					}
					eventMap[eventPair.Key] = eventData;
				}
			}

			return eventMap;
		}

		private class EventMapImpl : EventMap, IDictionary<ECustomGameMode, IReadOnlyDictionary<int, string>>
		{
			private Dictionary<ECustomGameMode, IReadOnlyDictionary<int, string>> mMap;

			public EventMapImpl()
			{
				mMap = new();
			}

			public override IReadOnlyDictionary<int, string> this[ECustomGameMode key] => mMap[key];

			public override IEnumerable<ECustomGameMode> Keys => mMap.Keys;

			public override IEnumerable<IReadOnlyDictionary<int, string>> Values => mMap.Values;

			public override int Count => mMap.Count;

			public void Add(ECustomGameMode key, IReadOnlyDictionary<int, string> value)
			{
				mMap.Add(key, value);
			}

			public void Clear()
			{
				mMap.Clear();
			}

			public override bool ContainsKey(ECustomGameMode key)
			{
				return mMap.ContainsKey(key);
			}

			public override IEnumerator<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>> GetEnumerator()
			{
				return mMap.GetEnumerator();
			}

			public bool Remove(ECustomGameMode key)
			{
				return mMap.Remove(key);
			}

			public override bool TryGetValue(ECustomGameMode key, [MaybeNullWhen(false)] out IReadOnlyDictionary<int, string> value)
			{
				return mMap.TryGetValue(key, out value);
			}

			#region Explicit interface implementations
			IReadOnlyDictionary<int, string> IDictionary<ECustomGameMode, IReadOnlyDictionary<int, string>>.this[ECustomGameMode key]
			{
				get => mMap[key];
				set => mMap[key] = value;
			}

			ICollection<ECustomGameMode> IDictionary<ECustomGameMode, IReadOnlyDictionary<int, string>>.Keys => mMap.Keys;

			ICollection<IReadOnlyDictionary<int, string>> IDictionary<ECustomGameMode, IReadOnlyDictionary<int, string>>.Values => mMap.Values;

			bool ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>.IsReadOnly => ((ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>)mMap).IsReadOnly;

			void ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>.Add(KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>> item)
			{
				((ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>)mMap).Add(item);
			}

			bool ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>.Contains(KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>> item)
			{
				return ((ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>)mMap).Contains(item);
			}

			void ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>.CopyTo(KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>[] array, int arrayIndex)
			{
				((ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>)mMap).CopyTo(array, arrayIndex);
			}

			bool ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>.Remove(KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>> item)
			{
				return ((ICollection<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>>)mMap).Remove(item);
			}
			#endregion
		}
	}

	internal abstract class EventMap : IReadOnlyDictionary<ECustomGameMode, IReadOnlyDictionary<int, string>>
	{
		public abstract IReadOnlyDictionary<int, string> this[ECustomGameMode key] { get; }

		public abstract IEnumerable<ECustomGameMode> Keys { get; }
		public abstract IEnumerable<IReadOnlyDictionary<int, string>> Values { get; }
		public abstract int Count { get; }

		public abstract bool ContainsKey(ECustomGameMode key);
		public abstract IEnumerator<KeyValuePair<ECustomGameMode, IReadOnlyDictionary<int, string>>> GetEnumerator();
		public abstract bool TryGetValue(ECustomGameMode key, [MaybeNullWhen(false)] out IReadOnlyDictionary<int, string> value);

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	internal struct EventData
	{
		public int Id;
		public HashSet<string> Names;
		public byte ModeMask;
		public List<SpawnData> SpawnData;
	}
}
