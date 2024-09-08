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

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Represents a range of numeric values
	/// </summary>
	internal readonly struct Range<T> : IEquatable<Range<T>> where T : struct, INumber<T>
	{
		public readonly T Min;
		public readonly T Max;

		public Range(T min, T max)
		{
			Min = min;
			Max = max;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Min, Max);
		}

		public override bool Equals([NotNullWhen(true)] object? obj)
		{
			return obj is Range<T> other && Equals(other);
		}

		public bool Equals(Range<T> other)
		{
			return Min.Equals(Min) && Max.Equals(Max);
		}

		public override string ToString()
		{
			return $"{Min}-{Max}";
		}

		/// <summary>
		/// Combines this range with another and returns a new range inclusive of both.
		/// </summary>
		/// <param name="other">The range to combine with</param>
		/// <returns>A new range inclusive of both ranges</returns>
		public Range<T> Combine(Range<T> other)
		{
			return new Range<T>(
				Min < other.Min ? Min : other.Min,
				Max > other.Max ? Max : other.Max);
		}

		public static bool operator ==(Range<T> a, Range<T> b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Range<T> a, Range<T> b)
		{
			return !a.Equals(b);
		}

		/// <summary>
		/// Adds the mins and maxes of two ranges together
		/// </summary>
		public static Range<T> operator +(Range<T> a, Range<T> b)
		{
			return new Range<T>(a.Min + b.Min, a.Max + b.Max);
		}
	}
}
