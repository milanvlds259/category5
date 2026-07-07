using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Vivox;
#if AUTH_PACKAGE_PRESENT
using Unity.Services.Authentication;
#endif
using TMPro;

public class VCManager : MonoBehaviour
{
    // I lowk copied this script from a YT guy bc it seems like it needs to exist but idk why nor what exactly it does

    public const string VoiceChannel = "VCLobby";

    static object m_Lock = new object();
    static VCManager m_Instance;

    [SerializeField]
    string _key;
    [SerializeField]
    string _issuer;
    [SerializeField]
    string _domain;
    [SerializeField]
    string _server;

    // I added this part
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private TMP_Dropdown inputDeviceDropdown;

    public static VCManager Instance
    {
        get
        {
            lock (m_Lock)
            {
                if (m_Instance == null)
                {
                    m_Instance = (VCManager)FindObjectOfType(typeof(VCManager));

                    if (m_Instance == null)
                    {
                        var singletonObject = new GameObject();
                        m_Instance = singletonObject.AddComponent<VCManager>();
                        singletonObject.name = typeof(VCManager).ToString() + " (Singleton)";
                    }
                }
                DontDestroyOnLoad(m_Instance.gameObject);
                return m_Instance;
            }
        }
    }

    async void Awake()
    {
        if (m_Instance != this && m_Instance != null)
        {
            Debug.LogWarning("Multiple VCManagers detected.");
            Destroy(this);
            return;
        }

        var options = new InitializationOptions();
        if (CheckManualCredentials())
        {
            options.SetVivoxCredentials(_server, _domain, _issuer, _key);
        }

        // This attaches the dropdown in the pause menu to the player prefab in order to get input devices
        PlayerVoice playerVoiceScript = playerPrefab.GetComponent<PlayerVoice>();
        if (playerVoiceScript != null)
        {
            playerVoiceScript.inputDropdown = inputDeviceDropdown;
        }

        await UnityServices.InitializeAsync(options);
#if AUTH_PACKAGE_PRESENT
        if (!CheckManualCredentials())
        {
            AuthenticationService.Instance.ClearSessionToken();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
#endif

        await VivoxService.Instance.InitializeAsync();
    }

    bool CheckManualCredentials()
    {
        return !(string.IsNullOrEmpty(_issuer) && string.IsNullOrEmpty(_domain) && string.IsNullOrEmpty(_server));
    }
}
