namespace Mavrix.Common.Dataverse
{
	/// <summary>
	/// Represents a Dataverse entity key expression used inside OData parentheses.
	/// </summary>
	public readonly struct DataverseKey
	{
		/// <summary>
		/// Initializes a key from a Dataverse record identifier.
		/// </summary>
		/// <param name="id">The Dataverse record identifier.</param>
		public DataverseKey(Guid id)
			: this(id.ToString())
		{
		}

		/// <summary>
		/// Initializes a key from a raw Dataverse key expression.
		/// </summary>
		/// <param name="keyExpression">The Dataverse key expression.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="keyExpression"/> is null, empty, or whitespace.</exception>
		public DataverseKey(string keyExpression)
		{
			if (string.IsNullOrWhiteSpace(keyExpression))
			{
				throw new ArgumentException("Key expression cannot be null or whitespace.", nameof(keyExpression));
			}

			KeyExpression = keyExpression;
		}

		/// <summary>
		/// Initializes a key from a single alternate key pair.
		/// </summary>
		/// <param name="keyName">The alternate key name.</param>
		/// <param name="keyValue">The alternate key value.</param>
		public DataverseKey(string keyName, string keyValue)
			: this((keyName, keyValue))
		{
		}

		/// <summary>
		/// Initializes a key from one or more alternate key pairs.
		/// </summary>
		/// <param name="keys">The alternate key name and value pairs.</param>
		/// <exception cref="ArgumentException">Thrown when no keys are provided or any key name is null, empty, or whitespace.</exception>
		/// <exception cref="ArgumentNullException">Thrown when any key value is <see langword="null"/>.</exception>
		public DataverseKey(params (string Name, string Value)[] keys)
		{
			if (keys is null || keys.Length == 0)
			{
				throw new ArgumentException("At least one alternate key must be provided.", nameof(keys));
			}

			var formattedKeys = new List<string>(keys.Length);
			foreach (var (name, value) in keys)
			{
				if (string.IsNullOrWhiteSpace(name))
				{
					throw new ArgumentException("Key name cannot be null or whitespace.", nameof(keys));
				}

				ArgumentNullException.ThrowIfNull(value);
				formattedKeys.Add($"{name}='{EscapeKeyValue(value)}'");
			}

			KeyExpression = string.Join(",", formattedKeys);
		}

		/// <summary>
		/// Gets the Dataverse key expression.
		/// </summary>
		public string KeyExpression { get; }

		/// <summary>
		/// Converts a record identifier into a Dataverse key.
		/// </summary>
		/// <param name="id">The Dataverse record identifier.</param>
		public static implicit operator DataverseKey(Guid id) => new(id);

		/// <summary>
		/// Returns the Dataverse key expression.
		/// </summary>
		/// <returns>The Dataverse key expression.</returns>
		public override string ToString() => KeyExpression;

		private static string EscapeKeyValue(string keyValue) => keyValue.Replace("'", "''");
	}
}