using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TanLuZhe
{
    /// <summary>Run state: score, deaths, checkpoints, win flag. Also owns the restart hotkey.</summary>
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("TanLuZhe/Game Manager")]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private bool _allowRestart = true;
        [SerializeField] private UnityEngine.InputSystem.Key _restartKey = UnityEngine.InputSystem.Key.R;

        private static GameManager _instance;
        private PlayerHealth _playerHealth;
        private int _score;
        private int _enemiesDefeated;
        private int _checkpointsReached;
        private bool _won;

        public static GameManager Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<GameManager>();
                return _instance;
            }
        }

        public int Score => _score;
        public int EnemiesDefeated => _enemiesDefeated;
        public int CheckpointsReached => _checkpointsReached;
        public int Deaths => _playerHealth != null ? _playerHealth.DeathCount : 0;
        public bool HasWon => _won;
        public PlayerHealth PlayerHealth => _playerHealth;

        public event Action StateChanged;
        public event Action Won;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
            RaiseChanged();
        }

        private void Update()
        {
            if (!_allowRestart) return;

            // Legacy input is disabled in this project, so read the key through the Input System.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard[_restartKey].wasPressedThisFrame) RestartLevel();
        }

        public void AddScore(int amount)
        {
            _score += amount;
            RaiseChanged();
        }

        public void ReportCheckpoint()
        {
            _checkpointsReached++;
            RaiseChanged();
        }

        public void ReportEnemyDefeated(EnemyController2D enemy)
        {
            _enemiesDefeated++;
            _score += 3;
            RaiseChanged();
        }

        public void Win()
        {
            if (_won) return;
            _won = true;
            RaiseChanged();
            Won?.Invoke();
        }

        public void RestartLevel()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        private void RaiseChanged() => StateChanged?.Invoke();
    }
}
