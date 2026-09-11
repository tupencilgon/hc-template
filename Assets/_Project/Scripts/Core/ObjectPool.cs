using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HC.Core
{
    /// <summary>
    /// Cho object trong pool tự reset state khi được lấy ra / trả về.
    /// Implement ở script trên prefab (vd: reset velocity, tắt trail, reset HP).
    /// </summary>
    public interface IPoolable
    {
        void OnSpawnFromPool();
        void OnReturnToPool();
    }

    /// <summary>
    /// Object pool dùng chung cho nhiều loại prefab, phân biệt bằng string key.
    /// Singleton — truy cập qua <see cref="Instance"/>, tự tạo nếu chưa có trong scene.
    /// Dùng <see cref="Get(string)"/> thay cho Instantiate, <see cref="Return(GameObject)"/> thay cho Destroy.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ObjectPool : MonoBehaviour
    {
        [Serializable]
        public class PoolConfig
        {
            [Tooltip("Key để gọi Get(). Nên khai hằng số một chỗ, đừng gõ tay chuỗi khắp nơi.")]
            public string key;
            public GameObject prefab;
            [Tooltip("Số instance tạo sẵn lúc khởi động, tránh khựng frame ở lần spawn đầu.")]
            [Min(0)] public int prewarmCount;
            [Tooltip("Số instance tối đa giữ lại trong pool. 0 = không giới hạn.")]
            [Min(0)] public int maxSize;
        }

        [SerializeField] private List<PoolConfig> configs = new List<PoolConfig>();
        [Tooltip("Giữ pool qua các lần đổi scene (DontDestroyOnLoad).")]
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private bool logWarnings = true;

        private sealed class PoolEntry
        {
            public GameObject Prefab;
            public Transform Root;
            public int MaxSize;
        }

        private readonly Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, PoolEntry> _registry = new Dictionary<string, PoolEntry>();
        private readonly Dictionary<GameObject, string> _keyOf = new Dictionary<GameObject, string>();
        private readonly HashSet<GameObject> _inPool = new HashSet<GameObject>();

        // Cache sẵn IPoolable lúc tạo instance: GetComponentsInChildren() cấp phát mảng mới mỗi lần gọi,
        // spawn liên tục là sinh rác GC ngay trong gameplay. Cache 1 lần, mỗi lần spawn 0 byte rác.
        // Đổi lại: component IPoolable thêm vào lúc runtime sẽ không được nhận diện.
        private readonly Dictionary<GameObject, IPoolable[]> _poolablesOf = new Dictionary<GameObject, IPoolable[]>();
        private static readonly IPoolable[] EmptyPoolables = new IPoolable[0];

        private static ObjectPool _instance;
        private static bool _isQuitting;

        public static ObjectPool Instance
        {
            get
            {
                if (_isQuitting) return null;
                if (_instance != null) return _instance;

                _instance = FindFirstObjectByType<ObjectPool>();
                if (_instance == null)
                {
                    var go = new GameObject("[ObjectPool]");
                    _instance = go.AddComponent<ObjectPool>();
                }
                return _instance;
            }
        }

        // Enter Play Mode không reload domain => static không tự reset. Dòng này lo việc đó.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _isQuitting = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            for (int i = 0; i < configs.Count; i++)
            {
                var c = configs[i];
                RegisterPool(c.key, c.prefab, c.prewarmCount, c.maxSize);
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        /// <summary>
        /// Khai báo 1 pool lúc runtime. Gọi lại với key đã tồn tại sẽ bị bỏ qua.
        /// </summary>
        public void RegisterPool(string key, GameObject prefab, int prewarmCount = 0, int maxSize = 0)
        {
            if (string.IsNullOrEmpty(key))
            {
                Warn("RegisterPool: key rỗng, bỏ qua.");
                return;
            }
            if (prefab == null)
            {
                Warn($"RegisterPool: prefab của key \"{key}\" bị null, bỏ qua.");
                return;
            }

            if (_registry.TryGetValue(key, out var existing))
            {
                if (existing.Prefab != prefab)
                {
                    Warn($"RegisterPool: key \"{key}\" đã gắn với prefab \"{existing.Prefab.name}\", " +
                         $"bỏ qua prefab mới \"{prefab.name}\".");
                }
                return;
            }

            var root = new GameObject($"Pool_{key}").transform;
            root.SetParent(transform, false);

            var entry = new PoolEntry { Prefab = prefab, Root = root, MaxSize = maxSize };
            var queue = new Queue<GameObject>(Mathf.Max(prewarmCount, 4));
            _registry[key] = entry;
            _pools[key] = queue;

            for (int i = 0; i < prewarmCount; i++)
            {
                var obj = CreateInstance(key, entry);
                obj.SetActive(false);
                queue.Enqueue(obj);
                _inPool.Add(obj);
            }
        }

        public bool HasPool(string key)
        {
            return !string.IsNullOrEmpty(key) && _registry.ContainsKey(key);
        }

        /// <summary>Số instance đang nằm sẵn trong pool (chưa được dùng).</summary>
        public int CountInactive(string key)
        {
            return _pools.TryGetValue(key, out var queue) ? queue.Count : 0;
        }

        public GameObject Get(string key)
        {
            return Get(key, Vector3.zero, Quaternion.identity, null);
        }

        public GameObject Get(string key, Vector3 position, Quaternion rotation)
        {
            return Get(key, position, rotation, null);
        }

        /// <summary>
        /// Lấy 1 object từ pool. Hết object thì Instantiate mới. Key chưa đăng ký thì trả null.
        /// </summary>
        public GameObject Get(string key, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (!_registry.TryGetValue(key, out var entry))
            {
                Warn($"Get: chưa đăng ký pool nào cho key \"{key}\". " +
                     "Thêm vào list Configs trong Inspector hoặc gọi RegisterPool() trước.");
                return null;
            }

            var queue = _pools[key];
            GameObject obj = null;

            while (queue.Count > 0)
            {
                var candidate = queue.Dequeue();
                if (candidate == null) continue; // đã bị Destroy ở đâu đó, bỏ qua
                _inPool.Remove(candidate);
                obj = candidate;
                break;
            }

            if (obj == null) obj = CreateInstance(key, entry);

            var t = obj.transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);

            var poolables = GetPoolables(obj);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnSpawnFromPool();

            return obj;
        }

        /// <summary>Như Get() nhưng trả thẳng component T. Null nếu prefab không có T.</summary>
        public T Get<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            var obj = Get(key, position, rotation, parent);
            if (obj == null) return null;

            var comp = obj.GetComponent<T>();
            if (comp == null) Warn($"Get<{typeof(T).Name}>: prefab của key \"{key}\" không có component này.");
            return comp;
        }

        /// <summary>
        /// Trả object về pool thay vì Destroy. Object không thuộc pool nào sẽ bị Destroy kèm cảnh báo,
        /// để Return() luôn giữ đúng nghĩa "dọn object này đi".
        /// </summary>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            if (!_keyOf.TryGetValue(obj, out var key) || !_registry.TryGetValue(key, out var entry))
            {
                Warn($"Return: \"{obj.name}\" không thuộc pool nào — Destroy thay vì trả về pool.");
                Destroy(obj);
                return;
            }

            if (!_inPool.Add(obj))
            {
                Warn($"Return: \"{obj.name}\" đã nằm trong pool rồi, bỏ qua lần trả thứ hai.");
                return;
            }

            var poolables = GetPoolables(obj);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnReturnToPool();

            obj.SetActive(false);
            obj.transform.SetParent(entry.Root, false);

            var queue = _pools[key];
            if (entry.MaxSize > 0 && queue.Count >= entry.MaxSize)
            {
                // Pool đã đầy — huỷ luôn để không phình bộ nhớ.
                _inPool.Remove(obj);
                _keyOf.Remove(obj);
                _poolablesOf.Remove(obj);
                Destroy(obj);
                return;
            }

            queue.Enqueue(obj);
        }

        /// <summary>Trả về pool sau delaySeconds giây (vd: VFX, đạn tự huỷ).</summary>
        public void Return(GameObject obj, float delaySeconds)
        {
            if (obj == null) return;
            if (delaySeconds <= 0f)
            {
                Return(obj);
                return;
            }
            StartCoroutine(ReturnDelayed(obj, delaySeconds));
        }

        private IEnumerator ReturnDelayed(GameObject obj, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            if (obj != null && !_inPool.Contains(obj)) Return(obj);
        }

        /// <summary>Thu hồi mọi object đang hoạt động của 1 pool (vd: lúc restart level).</summary>
        public void ReturnAll(string key)
        {
            if (!_registry.ContainsKey(key)) return;

            var actives = new List<GameObject>();
            foreach (var pair in _keyOf)
            {
                if (pair.Value != key) continue;
                if (pair.Key == null || _inPool.Contains(pair.Key)) continue;
                actives.Add(pair.Key);
            }

            for (int i = 0; i < actives.Count; i++) Return(actives[i]);
        }

        /// <summary>Thu hồi mọi object đang hoạt động của tất cả pool.</summary>
        public void ReturnAll()
        {
            var keys = new List<string>(_registry.Keys);
            for (int i = 0; i < keys.Count; i++) ReturnAll(keys[i]);
        }

        /// <summary>Xoá hẳn 1 pool và Destroy mọi instance của nó.</summary>
        public void Clear(string key)
        {
            if (!_registry.TryGetValue(key, out var entry)) return;

            var owned = new List<GameObject>();
            foreach (var pair in _keyOf)
            {
                if (pair.Value == key) owned.Add(pair.Key);
            }

            for (int i = 0; i < owned.Count; i++)
            {
                _keyOf.Remove(owned[i]);
                _inPool.Remove(owned[i]);
                _poolablesOf.Remove(owned[i]);
                if (owned[i] != null) Destroy(owned[i]);
            }

            if (entry.Root != null) Destroy(entry.Root.gameObject);
            _pools.Remove(key);
            _registry.Remove(key);
        }

        public void ClearAll()
        {
            var keys = new List<string>(_registry.Keys);
            for (int i = 0; i < keys.Count; i++) Clear(keys[i]);
        }

        private GameObject CreateInstance(string key, PoolEntry entry)
        {
            var obj = Instantiate(entry.Prefab, entry.Root);
            obj.name = $"{entry.Prefab.name}_{_keyOf.Count}";
            _keyOf[obj] = key;
            _poolablesOf[obj] = obj.GetComponentsInChildren<IPoolable>(true);
            return obj;
        }

        private IPoolable[] GetPoolables(GameObject obj)
        {
            return _poolablesOf.TryGetValue(obj, out var poolables) ? poolables : EmptyPoolables;
        }

        // Object đang active bị scene unload nuốt mất sẽ để lại key rác — dọn sau mỗi lần load scene.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var dead = new List<GameObject>();
            foreach (var pair in _keyOf)
            {
                if (pair.Key == null) dead.Add(pair.Key);
            }

            for (int i = 0; i < dead.Count; i++)
            {
                _keyOf.Remove(dead[i]);
                _inPool.Remove(dead[i]);
                _poolablesOf.Remove(dead[i]);
            }
        }

        private void Warn(string message)
        {
            if (logWarnings) Debug.LogWarning($"[ObjectPool] {message}", this);
        }
    }
}
