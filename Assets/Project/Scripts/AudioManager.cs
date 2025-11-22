using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;
    [SerializeField] private int initialPoolSize = 10;
    [SerializeField] private int maxPoolSize = 20;

    private List<AudioSource> _pool;
    private GameObject _poolContainer;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePool()
    {
        _pool = new List<AudioSource>();
        _poolContainer = new GameObject("AudioPool");
        _poolContainer.transform.SetParent(transform);

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewSource();
        }
    }

    private AudioSource CreateNewSource()
    {
        GameObject go = new GameObject($"AudioSource_{_pool.Count}");
        go.transform.SetParent(_poolContainer.transform);
        
        AudioSource source = go.AddComponent<AudioSource>();
        

        if (outputMixerGroup == null)
        {
            AudioMixer mixer = Resources.Load<AudioMixer>("MainMixer");
            if (mixer != null)
            {
                var groups = mixer.FindMatchingGroups("Master");
                if (groups.Length > 0) outputMixerGroup = groups[0];
            }
        }

        if (outputMixerGroup != null)
        {
            source.outputAudioMixerGroup = outputMixerGroup;
        }
        else
        {
            Debug.LogWarning("AudioManager: No Output Mixer Group assigned! Sound will play raw.");
        }

        source.playOnAwake = false;
        
        go.SetActive(false);
        _pool.Add(source);
        
        return source;
    }

    public void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSource();
        if (source != null)
        {
            source.gameObject.SetActive(true);
            source.volume = volume;
            source.clip = clip;
            source.Play();
            
            // Desactivar al terminar
            StartCoroutine(DisableSourceDelayed(source, clip.length));
        }
    }

    private AudioSource GetAvailableSource()
    {
        // Buscar inactivo
        foreach (var source in _pool)
        {
            if (!source.gameObject.activeInHierarchy)
            {
                return source;
            }
        }

        // Crear nuevo si es necesario
        if (_pool.Count < maxPoolSize)
        {
            return CreateNewSource();
        }

        Debug.LogWarning("AudioManager: Audio pool exhausted.");
        return null;
    }

    private System.Collections.IEnumerator DisableSourceDelayed(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay + 0.1f);
        if (source != null)
        {
            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
        }
    }

    // Asignar mixer dinámicamente
    public void SetMixerGroup(AudioMixerGroup group)
    {
        outputMixerGroup = group;
        foreach (var source in _pool)
        {
            if (source != null) source.outputAudioMixerGroup = group;
        }
    }

    private void Start()
    {
        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {

        if (outputMixerGroup == null)
        {
            Debug.LogError("❌ [AudioManager] CRITICAL: No Output Mixer Group assigned! \n" +
                           "Fix: Select 'AudioManager' in scene -> Drag 'MainMixer' (Master group) to 'Output Mixer Group'.");
        }
        else
        {

            if (outputMixerGroup.audioMixer.GetFloat("MasterVolume", out float val))
            {
                Debug.Log($"✅ [AudioManager] Configuration OK. MasterVolume is exposed (Current: {val}dB).");
            }
            else
            {
                Debug.LogError("❌ [AudioManager] CRITICAL: 'MasterVolume' parameter is NOT exposed! Volume slider will fail.\n" +
                               "Fix: Open Audio Mixer -> Select 'Master' Group -> Right Click 'Volume' in Inspector -> 'Expose Volume' -> Rename parameter to 'MasterVolume'.");
            }
        }
    }
}
