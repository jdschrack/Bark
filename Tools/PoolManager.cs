using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bark.Tools
{
    /// <summary>
    /// Centralized manager for object pools.
    /// Provides a registry for named pools and cleanup utilities.
    /// </summary>
    public static class PoolManager
    {
        private static readonly Dictionary<string, IPool> pools = new Dictionary<string, IPool>();

        /// <summary>
        /// Gets an existing pool or creates a new one with the given key.
        /// </summary>
        /// <typeparam name="T">Component type to pool</typeparam>
        /// <param name="key">Unique identifier for the pool</param>
        /// <param name="createFunc">Factory function to create new instances</param>
        /// <param name="onGet">Called when an object is retrieved from the pool</param>
        /// <param name="onRelease">Called when an object is returned to the pool</param>
        /// <param name="initialSize">Number of objects to pre-populate</param>
        /// <param name="maxSize">Maximum pool size</param>
        /// <returns>The pool for the given key</returns>
        public static ObjectPool<T> GetOrCreatePool<T>(
            string key,
            Func<T> createFunc,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            int initialSize = 0,
            int maxSize = 20) where T : Component
        {
            if (pools.TryGetValue(key, out var existingPool))
            {
                if (existingPool is ObjectPool<T> typedPool)
                {
                    return typedPool;
                }
                // Type mismatch - log error and return null to avoid InvalidCastException
                Debug.LogError($"[PoolManager] Pool '{key}' exists but has different type. Expected ObjectPool<{typeof(T).Name}>");
                return null;
            }

            var newPool = new ObjectPool<T>(createFunc, onGet, onRelease, initialSize, maxSize);
            pools[key] = newPool;
            return newPool;
        }

        /// <summary>
        /// Gets an existing pool by key.
        /// </summary>
        /// <returns>The pool if found and type matches, null otherwise</returns>
        public static ObjectPool<T> GetPool<T>(string key) where T : Component
        {
            if (pools.TryGetValue(key, out var pool))
            {
                if (pool is ObjectPool<T> typedPool)
                {
                    return typedPool;
                }
                Debug.LogError($"[PoolManager] Pool '{key}' exists but has different type. Expected ObjectPool<{typeof(T).Name}>");
                return null;
            }
            return null;
        }

        /// <summary>
        /// Removes and clears a specific pool.
        /// </summary>
        public static void RemovePool(string key)
        {
            if (pools.TryGetValue(key, out var pool))
            {
                pool.Clear();
                pools.Remove(key);
            }
        }

        /// <summary>
        /// Clears all pools and removes them from the registry.
        /// Call this on scene unload or mod cleanup.
        /// </summary>
        public static void ClearAll()
        {
            foreach (var pool in pools.Values)
            {
                pool.Clear();
            }
            pools.Clear();
        }

        /// <summary>
        /// Gets the number of registered pools.
        /// </summary>
        public static int PoolCount => pools.Count;
    }
}
