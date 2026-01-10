using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bark.Tools
{
    /// <summary>
    /// Non-generic interface to manage pools without knowing their generic type.
    /// Avoids reflection in PoolManager.ClearAll().
    /// </summary>
    public interface IPool
    {
        void Clear();
        int Count { get; }
    }

    /// <summary>
    /// Generic object pool for Unity Components.
    /// Reduces runtime allocations by reusing objects instead of instantiate/destroy cycles.
    /// </summary>
    /// <typeparam name="T">Component type to pool</typeparam>
    public class ObjectPool<T> : IPool where T : Component
    {
        private readonly Queue<T> pool = new Queue<T>();
        private readonly Func<T> createFunc;
        private readonly Action<T> onGet;
        private readonly Action<T> onRelease;
        private readonly int maxSize;

        public int Count => pool.Count;

        /// <summary>
        /// Creates a new object pool.
        /// </summary>
        /// <param name="createFunc">Factory function to create new instances</param>
        /// <param name="onGet">Called when an object is retrieved from the pool</param>
        /// <param name="onRelease">Called when an object is returned to the pool</param>
        /// <param name="initialSize">Number of objects to pre-populate</param>
        /// <param name="maxSize">Maximum pool size (excess objects are destroyed)</param>
        public ObjectPool(Func<T> createFunc, Action<T> onGet = null,
                          Action<T> onRelease = null, int initialSize = 0,
                          int maxSize = 20)
        {
            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            this.onGet = onGet;
            this.onRelease = onRelease;
            this.maxSize = maxSize;

            // Pre-populate pool
            for (int i = 0; i < initialSize; i++)
            {
                var obj = createFunc();
                if (obj != null)
                {
                    onRelease?.Invoke(obj);
                    pool.Enqueue(obj);
                }
            }
        }

        /// <summary>
        /// Gets an object from the pool, or creates a new one if the pool is empty.
        /// </summary>
        public T Get()
        {
            T obj;
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
                // Handle case where pooled object was destroyed externally
                if (obj == null)
                {
                    obj = createFunc();
                }
            }
            else
            {
                obj = createFunc();
            }

            onGet?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// Returns an object to the pool for reuse.
        /// If the pool is at max capacity, the object is destroyed instead.
        /// </summary>
        public void Release(T obj)
        {
            if (obj == null) return;

            if (pool.Count < maxSize)
            {
                onRelease?.Invoke(obj);
                pool.Enqueue(obj);
            }
            else
            {
                // Pool is full, destroy the object
                if (obj.gameObject != null)
                {
                    UnityEngine.Object.Destroy(obj.gameObject);
                }
            }
        }

        /// <summary>
        /// Clears the pool and destroys all pooled objects.
        /// </summary>
        public void Clear()
        {
            while (pool.Count > 0)
            {
                var obj = pool.Dequeue();
                if (obj != null && obj.gameObject != null)
                {
                    UnityEngine.Object.Destroy(obj.gameObject);
                }
            }
        }
    }
}
