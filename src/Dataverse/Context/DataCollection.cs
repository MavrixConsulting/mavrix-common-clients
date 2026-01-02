using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Mavrix.Common.Dataverse.Context
{
	/// <summary>
	/// Case-insensitive key/value container for Dataverse entity data and formatted values.
	/// </summary>
	/// <remarks>
	/// Values are stored as <see cref="object"/> and retrieved via indexer or helper methods; no conversion is performed.
	/// </remarks>
	public class DataCollection : IEnumerable<KeyValuePair<string, object?>>
	{
		private readonly Dictionary<string, object?> _data = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>An enumerator for the collection.</returns>
		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _data.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <summary>
		/// Gets or sets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The attribute logical name.</param>
		/// <returns>The value for the specified key.</returns>
		public object? this[string key]
		{
			get => _data[key];
			set => _data[key] = value;
		}

		/// <summary>
		/// Determines whether the collection contains the specified key.
		/// </summary>
		/// <param name="key">The attribute logical name.</param>
		/// <returns><see langword="true"/> if the key exists; otherwise <see langword="false"/>.</returns>
		public bool Contains(string key) => _data.ContainsKey(key);

		/// <summary>
		/// Attempts to get the value associated with the specified key.
		/// </summary>
		/// <param name="key">The attribute logical name.</param>
		/// <param name="value">When this method returns, contains the value if found; otherwise <see langword="null"/>.</param>
		/// <returns><see langword="true"/> if the key exists; otherwise <see langword="false"/>.</returns>
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) =>
			_data.TryGetValue(key, out value);

		/// <summary>
		/// Gets the value associated with the specified key if it exists and can be cast to the requested type.
		/// </summary>
		/// <typeparam name="T">The expected type of the value.</typeparam>
		/// <param name="key">The attribute logical name.</param>
		/// <returns>The value cast to <typeparamref name="T"/> if found and compatible; otherwise <see langword="default"/>.</returns>
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
