﻿using System;
using System.Collections.Generic;

namespace XMoat.Common
{
    public class CircularBuffer
    {
        public int ChunkSize = 8 * 1024;

        private readonly Queue<byte[]> bufferQueue = new Queue<byte[]>();

        private readonly Queue<byte[]> bufferCache = new Queue<byte[]>();

        public int LastIndex { get; set; }

        public int FirstIndex { get; set; }
		
        private byte[] lastBuffer;

        public CircularBuffer()
        {
            this.AddLast();
        }

        public CircularBuffer(int chunkSize)
        {
            this.ChunkSize = chunkSize;
            this.AddLast();
        }

        /// <summary>
        /// 当前所有缓存块的总数据
        /// </summary>
        public int TotalSize
        {
            get
            {
                int c = 0;
                if (this.bufferQueue.Count == 0)
                {
                    c = 0;
                }
                else
                {
                    c = (this.bufferQueue.Count - 1) * ChunkSize + this.LastIndex - this.FirstIndex;
                }
                if (c < 0)
                {
                    Log.Error("TBuffer count < 0: {0}, {1}, {2}".Fmt(this.bufferQueue.Count, this.LastIndex, this.FirstIndex));
                }
                return c;
            }
        }

        public void AddLast()
        {
            byte[] buffer;
            //从缓存中取出一个区块
            if (this.bufferCache.Count > 0)
                buffer = this.bufferCache.Dequeue();
            else
                buffer = new byte[ChunkSize];

            //区块放入队尾
            this.bufferQueue.Enqueue(buffer);
            this.lastBuffer = buffer;
        }

        public void RemoveFirst()
        {
            //将队首区块移入缓存待用
            this.bufferCache.Enqueue(bufferQueue.Dequeue());
        }

        public byte[] First
        {
            get
            {
                if (this.bufferQueue.Count == 0)
                    this.AddLast();

                return this.bufferQueue.Peek();
            }
        }

        public byte[] Last
        {
            get
            {
                if (this.bufferQueue.Count == 0)
                    this.AddLast();

                return this.lastBuffer;
            }
        }
        /// <summary>
        /// 接收时从队首开始
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="count"></param>
        public void RecvFrom(byte[] buffer, int count)
        {
            if (this.TotalSize < count)
            {
                throw new Exception($"bufferList size < n, bufferList: {this.TotalSize} buffer length: {buffer.Length} {count}");
            }
            int alreadyCopyCount = 0;
            while (alreadyCopyCount < count)
            {
                int n = count - alreadyCopyCount;
                if (ChunkSize - this.FirstIndex > n)
                {
                    Array.Copy(this.First, this.FirstIndex, buffer, alreadyCopyCount, n);
                    this.FirstIndex += n;
                    alreadyCopyCount += n;
                }
                else
                {
                    Array.Copy(this.First, this.FirstIndex, buffer, alreadyCopyCount, ChunkSize - this.FirstIndex);
                    alreadyCopyCount += ChunkSize - this.FirstIndex;
                    this.FirstIndex = 0;
                    this.RemoveFirst();
                }
            }
        }
        /// <summary>
        /// 发送时塞在队尾
        /// </summary>
        /// <param name="buffer"></param>
        public void SendTo(byte[] buffer)
        {
            int alreadyCopyCount = 0;
            while (alreadyCopyCount < buffer.Length)
            {
                if (this.LastIndex == ChunkSize)
                {
                    this.AddLast();
                    this.LastIndex = 0;
                }

                int n = buffer.Length - alreadyCopyCount;
                if (ChunkSize - this.LastIndex > n)
                {
                    Array.Copy(buffer, alreadyCopyCount, this.lastBuffer, this.LastIndex, n);
                    this.LastIndex += buffer.Length - alreadyCopyCount;
                    alreadyCopyCount += n;
                }
                else
                {
                    Array.Copy(buffer, alreadyCopyCount, this.lastBuffer, this.LastIndex, ChunkSize - this.LastIndex);
                    alreadyCopyCount += ChunkSize - this.LastIndex;
                    this.LastIndex = ChunkSize;
                }
            }
        }

        public void SendTo(byte[] buffer, int offset, int count)
        {
            int alreadyCopyCount = 0;
            while (alreadyCopyCount < count)
            {
                if (this.LastIndex == ChunkSize)
                {
                    this.AddLast();
                    this.LastIndex = 0;
                }

                int n = count - alreadyCopyCount;
                if (ChunkSize - this.LastIndex > n)
                {
                    Array.Copy(buffer, alreadyCopyCount + offset, this.lastBuffer, this.LastIndex, n);
                    this.LastIndex += count - alreadyCopyCount;
                    alreadyCopyCount += n;
                }
                else
                {
                    Array.Copy(buffer, alreadyCopyCount + offset, this.lastBuffer, this.LastIndex, ChunkSize - this.LastIndex);
                    alreadyCopyCount += ChunkSize - this.LastIndex;
                    this.LastIndex = ChunkSize;
                }
            }
        }
    }
}