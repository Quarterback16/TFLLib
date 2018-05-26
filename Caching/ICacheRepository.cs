using System;

namespace TFLLib.Caching
{
	/// <summary>
	/// Defines the methods and properties that are required for a Cache Repository.
	/// </summary>
	public interface ICacheRepository
	{
		bool IsActive { get; set; }

		/// <summary>
		/// Add an object into the cache under a unique key.
		/// </summary>
		/// <typeparam name="T">Type of object.</typeparam>
		/// <param name="key">Key value.</param>
		/// <param name="o">Object to add into the cache.</param>
		/// <param name="timeToLive">How long the object should live in the cache.</param>
		/// <param name="dependsOnKey">Key this object depends on.</param>
		/// <returns><c>true</c> if the object is added into the cache; otherwise, <c>false</c> if an object already exists under that key.</returns>
		bool Add<T>(string key, T o, TimeSpan? timeToLive = null, string dependsOnKey = null);

		/// <summary>
		/// Set an object into the cache under a unique key.
		/// </summary>
		/// <typeparam name="T">Type of object.</typeparam>
		/// <param name="key">Key value.</param>
		/// <param name="o">Object to set into the cache.</param>
		/// <param name="timeToLive">How long the object should live in the cache.</param>
		/// <param name="dependsOnKey">Key this object depends on.</param>
		void Set<T>(string key, T o, TimeSpan? timeToLive = null, string dependsOnKey = null);

		/// <summary>
		/// Remove an object from the cache.
		/// </summary>
		/// <param name="key">Key value.</param>
		void Remove(string key);

		/// <summary>
		/// Try to get a stored object.
		/// </summary>
		/// <typeparam name="T">Type of object.</typeparam>
		/// <param name="key">Key value.</param>
		/// <param name="value">Stored object.</param>
		/// <returns>Stored object as type.</returns>
		bool TryGet<T>(string key, out T value);
	}
}