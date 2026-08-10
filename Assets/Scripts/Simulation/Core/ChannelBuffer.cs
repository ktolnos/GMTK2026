using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Chronomancers.Sim
{
    public interface IChannelBuffer
    {
        int ChannelId { get; }
        int Count { get; }
        Type StateType { get; }

        void Grow(int toCount);
        void Truncate(int toCount);
        void WriteTo(BinaryWriter w);
        void ReadFrom(BinaryReader r);
    }

    /// <summary>
    /// One component's state, one entry per sample, parallel to its body's sample times.
    /// <para>
    /// All of a body's channels share a single sample index space, because a body's components are
    /// always recorded together on the same step. So resolving which span is authoritative happens
    /// once per body per frame, not once per channel, and the whole thing is a columnar blob that
    /// serializes as a memcpy.
    /// </para>
    /// <para>
    /// <typeparamref name="TState"/> is constrained to <c>unmanaged</c> deliberately: a class per
    /// sample would allocate hundreds of thousands of objects per take. Prefer <c>byte</c> over
    /// <c>bool</c> in state structs so the on-disk layout stays predictable.
    /// </para>
    /// </summary>
    public sealed class ChannelBuffer<TState> : IChannelBuffer where TState : unmanaged
    {
        TState[] _items = new TState[64];
        int _count;

        public ChannelBuffer(int channelId) => ChannelId = channelId;

        public int ChannelId { get; }
        public int Count => _count;
        public Type StateType => typeof(TState);

        public ref TState this[int index] => ref _items[index];

        public void Set(int index, in TState value) => _items[index] = value;

        public void Grow(int toCount)
        {
            if (toCount <= _count) return;
            if (toCount > _items.Length)
                Array.Resize(ref _items, Math.Max(toCount, _items.Length * 2));
            // New slots are default so a channel registered mid-take back-fills cleanly.
            Array.Clear(_items, _count, toCount - _count);
            _count = toCount;
        }

        public void Truncate(int toCount)
        {
            if (toCount >= _count) return;
            Array.Clear(_items, toCount, _count - toCount);
            _count = toCount;
        }

        public void WriteTo(BinaryWriter w)
        {
            var bytes = MemoryMarshal.AsBytes(new ReadOnlySpan<TState>(_items, 0, _count)).ToArray();
            w.Write(_count);
            w.Write(bytes.Length);
            w.Write(bytes, 0, bytes.Length);
        }

        public void ReadFrom(BinaryReader r)
        {
            var count = r.ReadInt32();
            var byteLength = r.ReadInt32();
            var bytes = r.ReadBytes(byteLength);
            if (bytes.Length != byteLength)
                throw new EndOfStreamException($"channel {ChannelId}: truncated payload");
            _items = new TState[Math.Max(count, 1)];
            _count = count;
            if (count > 0)
                MemoryMarshal.Cast<byte, TState>(bytes).CopyTo(new Span<TState>(_items, 0, count));
        }
    }

    /// <summary>
    /// Maps channel ids to state types so a saved timeline can be reconstructed. Register every
    /// channel once at startup, from the same place that assigns the ids.
    /// </summary>
    public sealed class ChannelRegistry
    {
        readonly Dictionary<int, Func<int, IChannelBuffer>> _factories = new();

        public void Register<TState>(int channelId) where TState : unmanaged =>
            _factories[channelId] = id => new ChannelBuffer<TState>(id);

        public bool IsRegistered(int channelId) => _factories.ContainsKey(channelId);

        public IChannelBuffer Create(int channelId) =>
            _factories.TryGetValue(channelId, out var factory)
                ? factory(channelId)
                : throw new InvalidOperationException(
                    $"channel {channelId} is not registered; cannot load a timeline containing it");
    }
}
