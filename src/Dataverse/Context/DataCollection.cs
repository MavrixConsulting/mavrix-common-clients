using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Mavrix.Common.Dataverse.Context
{
	public class DataCollection : IEnumerable<KeyValuePair<string, object?>>
	{
		private readonly Dictionary<string, object?> _data = new(StringComparer.OrdinalIgnoreCase);

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _data.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public object? this[string key]
		{
			get => _data[key];
			set => _data[key] = value;
		}

		public bool Contains(string key) => _data.ContainsKey(key);

		public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) =>
			_data.TryGetValue(key, out value);

		public T? GetValueOrNull<T>(string key)
		{
			ArgumentNullException.ThrowIfNull(key);

			if (_data.TryGetValue(key, out var value) && value is T typed)
			{
				return typed;
			}
			return default;
		}
	}
}
