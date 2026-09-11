using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator
{
    /// <summary>
    /// An immutable array with structural (sequence) equality, so that it can be a member of
    /// records that flow through the incremental generator pipeline and still be cached correctly.
    /// </summary>
    public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
        where T : IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

        private readonly ImmutableArray<T> _array;

        public EquatableArray(ImmutableArray<T> array)
        {
            _array = array.IsDefault ? ImmutableArray<T>.Empty : array;
        }

        public static EquatableArray<T> From(IEnumerable<T> items) => new(ImmutableArray.CreateRange(items));

        public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

        public int Count => AsImmutableArray().Length;

        public int Length => Count;

        public T this[int index] => AsImmutableArray()[index];

        public bool Equals(EquatableArray<T> other)
        {
            var a = AsImmutableArray();
            var b = other.AsImmutableArray();
            if (a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var item in AsImmutableArray())
                {
                    hash = (hash * 31) + (item is null ? 0 : item.GetHashCode());
                }

                return hash;
            }
        }

        public ImmutableArray<T>.Enumerator GetEnumerator() => AsImmutableArray().GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)AsImmutableArray()).GetEnumerator();

        public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

        public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
    }
}
