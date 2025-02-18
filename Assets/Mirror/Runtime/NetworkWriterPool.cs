using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Mirror
{
    /// <summary>Pooled NetworkWriter, automatically returned to pool when using 'using'</summary>
    public sealed class PooledNetworkWriter : NetworkWriter
    {
        public void Recycle()
        {
            NetworkWriterPool.Recycle(this);
        }
    }

    /// <summary>Pool of NetworkWriters to avoid allocations.</summary>
    /// <summary>Pool of NetworkWriters to avoid allocations.</summary>
    public static class NetworkWriterPool
    {
        // reuse Pool<T>
        // we still wrap it in NetworkWriterPool.Get/Recycle so we can reset the
        // position before reusing.
        // this is also more consistent with NetworkReaderPool where we need to
        // assign the internal buffer before reusing.
        static readonly Pool<PooledNetworkWriter> Pool = new Pool<PooledNetworkWriter>(
            () => new PooledNetworkWriter(),
            // initial capacity to avoid allocations in the first few frames
            // 1000 * 1200 bytes = around 1 MB.
            1000
        );

        /// <summary>Get a writer from the pool. Creates new one if pool is empty.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledNetworkWriter GetWriter()
        {
            // grab from pool & reset position
            lock (Pool)
            {
                PooledNetworkWriter writer = Pool.Take();
                writer.Reset();
                return writer;
            }


        }

        /// <summary>Return a writer to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Recycle(PooledNetworkWriter writer)
        {
            lock (Pool)
            {
                Pool.Return(writer);
            }
        }
    }
}
