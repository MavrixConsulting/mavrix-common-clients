using Mavrix.Common.Dataverse.CustomAttributes;
using Mavrix.Common.Dataverse.DTO;
using Mavrix.Common.Dataverse.Serialization;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.AttributeTypes
{
	/// <summary>
	/// Represents a Dataverse lookup reference that serializes to an <c>@odata.bind</c> path.
	/// </summary>
	/// <typeparam name="T">The Dataverse table type being referenced.</typeparam>
	[JsonConverter(typeof(DataverseLookupJsonConverterFactory))]
	public sealed class Lookup<T> where T : DataverseTable
	{
		private static readonly string SetName = ResolveSetName();

		/// <summary>
		/// Initializes a new lookup using a Dataverse record ID.
		/// </summary>
		/// <param name="id">The record ID.</param>
		public Lookup(Guid id)
			: this(id.ToString())
		{
		}

		/// <summary>
		/// Initializes a new lookup using a nullable Dataverse record ID.
		/// </summary>
		/// <param name="id">The record ID.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
		public Lookup(Guid? id)
			: this(id ?? throw new ArgumentNullException(nameof(id)))
		{
		}

		/// <summary>
		/// Initializes a new lookup using a raw key expression.
		/// </summary>
		/// <param name="keyExpression">The Dataverse key expression used inside the bind path.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="keyExpression"/> is null, empty, or whitespace.</exception>
		public Lookup(string keyExpression)
		{
			if (string.IsNullOrWhiteSpace(keyExpression))
			{
				throw new ArgumentException("Key expression cannot be null or whitespace.", nameof(keyExpression));
			}

			KeyExpression = keyExpression;
		}

		/// <summary>
		/// Initializes a new lookup using an alternate key name and value.
		/// </summary>
		/// <param name="keyName">The alternate key name.</param>
		/// <param name="keyValue">The alternate key value.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="keyName"/> is null, empty, or whitespace.</exception>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="keyValue"/> is <see langword="null"/>.</exception>
		public Lookup(string keyName, string keyValue)
		{
			if (string.IsNullOrWhiteSpace(keyName))
			{
				throw new ArgumentException("Key name cannot be null or whitespace.", nameof(keyName));
			}

			ArgumentNullException.ThrowIfNull(keyValue);

			KeyExpression = $"{keyName}='{EscapeKeyValue(keyValue)}'";
		}

		/// <summary>
		/// Gets the Dataverse key expression that is written inside the bind path.
		/// </summary>
		public string KeyExpression { get; }

		/// <summary>
		/// Builds the Dataverse <c>@odata.bind</c> path for this lookup.
		/// </summary>
		/// <returns>The formatted bind path.</returns>
		public string ToODataBind() => $"/{SetName}({KeyExpression})";

		/// <summary>
		/// Returns the Dataverse <c>@odata.bind</c> path for this lookup.
		/// </summary>
		/// <returns>The formatted bind path.</returns>
		public override string ToString() => ToODataBind();

		/// <summary>
		/// Converts a record ID to a lookup instance.
		/// </summary>
		/// <param name="id">The record ID.</param>
		/// <returns>A lookup targeting the specified record.</returns>
		public static implicit operator Lookup<T>(Guid id) => new(id.ToString());

		/// <summary>
		/// Converts a nullable record ID to a nullable lookup instance.
		/// </summary>
		/// <param name="id">The nullable record ID.</param>
		/// <returns>A lookup when the ID has a value; otherwise <see langword="null"/>.</returns>
		public static implicit operator Lookup<T>?(Guid? id) => id.HasValue ? new Lookup<T>(id.Value.ToString()) : null;

		private static string EscapeKeyValue(string keyValue) => keyValue.Replace("'", "''");

		private static string ResolveSetName()
		{
			var attribute = typeof(T).GetCustomAttribute<DataverseSetNameAttribute>() 
				?? throw new InvalidOperationException($"Type '{typeof(T).Name}' is missing DataverseSetNameAttribute.");
			return attribute.SetName;
		}
	}
}