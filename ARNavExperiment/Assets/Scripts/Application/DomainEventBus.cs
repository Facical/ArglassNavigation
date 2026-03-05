using System;
using System.Collections.Generic;
using UnityEngine;
using ARNavExperiment.Domain.Events;

namespace ARNavExperiment.Application
{
    /// <summary>
    /// 중앙 도메인 이벤트 버스. 동기 발행, 핸들러별 try-catch.
    /// DontDestroyOnLoad 싱글턴.
    /// </summary>
    public class DomainEventBus : MonoBehaviour
    {
        public static DomainEventBus Instance { get; private set; }

        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 도메인 이벤트 구독.
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : IDomainEvent
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            list.Add(handler);
        }

        /// <summary>
        /// 도메인 이벤트 구독 해제.
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : IDomainEvent
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
            {
                list.Remove(handler);
            }
        }

        /// <summary>
        /// 도메인 이벤트 동기 발행. 각 핸들러를 try-catch로 격리.
        /// </summary>
        public void Publish<T>(T evt) where T : IDomainEvent
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list)) return;

            // 반복 중 Unsubscribe 방지를 위해 복사본 사용
            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
            {
                try
                {
                    ((Action<T>)handler)(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DomainEventBus] Error handling {type.Name}: {ex}");
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                _handlers.Clear();
                Instance = null;
            }
        }
    }
}
