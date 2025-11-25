using System;
using System.Collections.Generic;
using UnityEngine;

/**
* 用于解决回调深度问题，很多函数一直 Action 深入回调的写法比较难看
*/

namespace Bear.Common
{
    /// <summary>
    /// 指明 payload 保存的值类型，避免 object 装箱。
    /// </summary>
    public enum ActionPayloadKind : byte
    {
        None,
        Bool,
        Int,
        Float,
        String
    }

    /// <summary>
    /// 用 struct 保存不同类型的 payload。
    /// </summary>
    public struct ActionPayload
    {
        public ActionPayloadKind Kind;

        public bool BoolValue;
        public int IntValue;
        public float FloatValue;
        public string StringValue;

        public static ActionPayload None() => new ActionPayload { Kind = ActionPayloadKind.None };
        public static ActionPayload FromBool(bool value) => new ActionPayload { Kind = ActionPayloadKind.Bool, BoolValue = value };
        public static ActionPayload FromInt(int value) => new ActionPayload { Kind = ActionPayloadKind.Int, IntValue = value };
        public static ActionPayload FromFloat(float value) => new ActionPayload { Kind = ActionPayloadKind.Float, FloatValue = value };
        public static ActionPayload FromString(string value) => new ActionPayload { Kind = ActionPayloadKind.String, StringValue = value };
        // 如需扩展其他 struct，可在此继续添加
    }

    /// <summary>
    /// Action + Payload 的值类型封装，避免 GC。
    /// </summary>
    public readonly struct ActionTask
    {
        readonly Delegate _callback;
        readonly ActionPayload _payload;

        public ActionTask(Action callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _payload = ActionPayload.None();
        }

        public ActionTask(Action<int> callback, in ActionPayload payload)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (payload.Kind != ActionPayloadKind.Int) throw new ArgumentException("payload must be Int", nameof(payload));
            _callback = callback;
            _payload = payload;
        }

        public ActionTask(Action<bool> callback, in ActionPayload payload)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (payload.Kind != ActionPayloadKind.Bool) throw new ArgumentException("payload must be Bool", nameof(payload));
            _callback = callback;
            _payload = payload;
        }

        public ActionTask(Action<float> callback, in ActionPayload payload)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (payload.Kind != ActionPayloadKind.Float) throw new ArgumentException("payload must be Float", nameof(payload));
            _callback = callback;
            _payload = payload;
        }

        public ActionTask(Action<string> callback, in ActionPayload payload)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (payload.Kind != ActionPayloadKind.String) throw new ArgumentException("payload must be String", nameof(payload));
            _callback = callback;
            _payload = payload;
        }

        public bool IsValid => _callback != null;

        public void Invoke()
        {
            switch (_payload.Kind)
            {
                case ActionPayloadKind.None:
                    (_callback as Action)?.Invoke();
                    break;
                case ActionPayloadKind.Int:
                    (_callback as Action<int>)?.Invoke(_payload.IntValue);
                    break;
                case ActionPayloadKind.Bool:
                    (_callback as Action<bool>)?.Invoke(_payload.BoolValue);
                    break;
                case ActionPayloadKind.Float:
                    (_callback as Action<float>)?.Invoke(_payload.FloatValue);
                    break;
                case ActionPayloadKind.String:
                    (_callback as Action<string>)?.Invoke(_payload.StringValue);
                    break;
                default:
                    throw new NotSupportedException($"Unhandled payload kind: {_payload.Kind}");
            }
        }
    }

    /// <summary>
    /// Action 调度器：线程安全入队，主线程批量执行。
    /// </summary>
    public sealed class ActionDispatcher
    {
        readonly Queue<ActionTask> _queue = new Queue<ActionTask>();
        readonly object _lock = new object();

        /// <summary>控制每次 Process 执行的 Action 数量，默认 128。</summary>
        public int MaxPerProcess { get; set; } = 128;

        public int Count
        {
            get
            {
                lock (_lock)
                    return _queue.Count;
            }
        }

        public void Enqueue(ActionTask task)
        {
            if (!task.IsValid)
                return;

            lock (_lock)
                _queue.Enqueue(task);
        }

        public void Enqueue(Action action) => Enqueue(new ActionTask(action));
        public void Enqueue(Action<bool> action, bool value) => Enqueue(new ActionTask(action, ActionPayload.FromBool(value)));
        public void Enqueue(Action<int> action, int value) => Enqueue(new ActionTask(action, ActionPayload.FromInt(value)));
        public void Enqueue(Action<float> action, float value) => Enqueue(new ActionTask(action, ActionPayload.FromFloat(value)));
        public void Enqueue(Action<string> action, string value) => Enqueue(new ActionTask(action, ActionPayload.FromString(value)));

        /// <summary>
        /// 每次执行一个 ActionTask。返回 true 表示执行成功，false 表示队列已空。
        /// </summary>
        public bool Step()
        {
            ActionTask task;
            lock (_lock)
            {
                if (_queue.Count == 0)
                    return false;
                task = _queue.Dequeue();
            }

            try
            {
                task.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            return true;
        }

        /// <summary>
        /// 在主线程调用，执行队列中的 Action（最多执行 MaxPerProcess 个）。
        /// </summary>
        public void Process()
        {
            int processed = 0;
            while (processed < MaxPerProcess)
            {
                if (!Step())
                    break;
                processed++;
            }
        }

        public void Clear()
        {
            lock (_lock)
                _queue.Clear();
        }
    }
}

