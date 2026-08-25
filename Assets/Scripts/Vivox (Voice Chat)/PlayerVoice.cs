using Unity.Netcode;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PlayerVoice : NetworkBehaviour
{
    // Most of these functions are taken from the Vivox documentation but I needed a YT video to make sense of it

    [SerializeField] private GameObject _playerHead;
    private Vector3 lastHeadPos;

    private int volumeNum = 12;

    private string channelName = "ProximityChat";
    private string testChannelName = "TestingChat";
    private string radioChannelName = "RadioChat";
    private bool inChannel = false;
    Channel3DProperties properties;

    private int clientId;

    private float _nextPosUpdate;

    public Toggle echoToggle;
    public Toggle muteToggle;
    public TMP_Dropdown inputDropdown;
    private List<VivoxInputDevice> availableDevices = new List<VivoxInputDevice>();

    private bool testingOn = false;
    private bool pttOn = false;

    private AudioSource radioSFX;
    public AudioClip radioClickBeep;
    public AudioClip radioClick;

    void Start()
    {
        echoToggle.onValueChanged.AddListener(OnEchoToggleChanged);
        muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
        radioSFX = GetComponent<AudioSource>();

        if (IsLocalPlayer)
        {
            InitializeAsync();

            VivoxService.Instance.LoggedIn += OnLoggedIn;
            VivoxService.Instance.LoggedOut += OnLoggedOut;
        }
    }

    // Does what it says on the tin
    async void InitializeAsync()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn && !AuthenticationService.Instance.IsAuthorized)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        await VivoxService.Instance.InitializeAsync();
        Debug.Log("Async Initialized.");

        LoginToVivoxAsync();
    }

    // Logs you in and gives you a name based on your client number
    public async void LoginToVivoxAsync()
    {
        if (IsLocalPlayer)
        {
            clientId = (int) GameObject.Find("NetworkManager").GetComponent<NetworkManager>().LocalClientId;

            LoginOptions options = new LoginOptions();
            options.DisplayName = "Client " + clientId;
            options.EnableTTS = true;
            await VivoxService.Instance.LoginAsync(options);

            PopulateInputDropdown();

            Join3DChannelAsync();
        }
    }

    // Puts you into the voice channel
    public async void Join3DChannelAsync()
    {
        await VivoxService.Instance.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, properties);
        VivoxService.Instance.SetOutputDeviceVolume(volumeNum);
        inChannel = true;
        JoinGroupChannelAsync();
        await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, channelName);
        Debug.Log("Joined 3D positional channel.");
    }

    // Puts you into a testing channel
    public async void JoinEchoChannelAsync()
    {
        await VivoxService.Instance.JoinEchoChannelAsync(testChannelName, ChatCapability.AudioOnly);
        VivoxService.Instance.SetOutputDeviceVolume(volumeNum);
        inChannel = true;
        Debug.Log("Joined Echo channel.");
    }

    public async void JoinGroupChannelAsync()
    {
        await VivoxService.Instance.JoinGroupChannelAsync(radioChannelName, ChatCapability.AudioOnly);
        VivoxService.Instance.SetOutputDeviceVolume(volumeNum);
        Debug.Log("Joined Group channel.");
    }

    // Leaves a given channel
    void LeaveChannel(string channelNameToLeave)
    {
        VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.All);
        VivoxService.Instance.LeaveChannelAsync(channelNameToLeave);
        Debug.Log("Left " + channelNameToLeave + " channel.");
    }

    // Update is called once per frame
    void Update()
    {
        if (inChannel && IsLocalPlayer)
        {
            // So it doesn't do this every frame
            if (Time.time > _nextPosUpdate && testingOn == false)
            {
                UpdatePlayerPos();
                _nextPosUpdate += 0.3f;
            }

            if (pttOn && Keyboard.current != null && testingOn == false)
            {
                if (Keyboard.current.cKey.wasPressedThisFrame)
                {
                    VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, channelName);
                    Debug.Log("Transmitting voice.");
                } else if (Keyboard.current.cKey.wasReleasedThisFrame)
                {
                    VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
                    Debug.Log("Not transmitting voice.");
                }
            }

            if (Keyboard.current != null && testingOn == false)
            {
                if (Keyboard.current.vKey.wasPressedThisFrame)
                {
                    radioSFX.PlayOneShot(radioClickBeep);
                    VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.All);
                    Debug.Log("Radio on.");
                } else if (Keyboard.current.vKey.wasReleasedThisFrame)
                {
                    radioSFX.PlayOneShot(radioClick);
                    VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, channelName);
                    Debug.Log("Radio off.");
                }
            }
        }
    }

    // Moves the positional audio to where the player is
    public void UpdatePlayerPos()
    {
        VivoxService.Instance.Set3DPosition(_playerHead, channelName);
        if (_playerHead.transform.position != lastHeadPos)
        {
            lastHeadPos = _playerHead.transform.position;
        }
    }

    // Tbh I copied this one, I think it helps if the player head object needs to be attached
    public void setPlayerHeadPos(GameObject playerHead)
    {
        if (_playerHead == null)
        {
            _playerHead = playerHead;
        }
    }

    private void OnLoggedIn()
    {
        if (VivoxService.Instance.IsLoggedIn)
        {
            Debug.Log("Client " + clientId + " logged in.");
        } else
        {
            Debug.Log("Could not log in.");
        }
    }

    private void OnLoggedOut()
    {
        inChannel = false;

        VivoxService.Instance.LeaveAllChannelsAsync();
        Debug.Log("Left all VC channels.");
        VivoxService.Instance.LogoutAsync();
        Debug.Log("Logged out.");
    }

    // Populates lists of the input devices for each player, adds the options to the dropdown, and checks for a change
    public void PopulateInputDropdown()
    {
        if (inputDropdown == null) return;

        inputDropdown.ClearOptions();

        if (VivoxService.Instance == null || VivoxService.Instance.AvailableInputDevices == null) return;

        List<string> deviceNames = new List<string>();
        foreach (var device in VivoxService.Instance.AvailableInputDevices)
        {
            availableDevices.Add(device);
            deviceNames.Add(device.DeviceName);
        }

        inputDropdown.AddOptions(deviceNames);
        Debug.Log("Input devices connected.");

        inputDropdown.onValueChanged.AddListener(OnDeviceSelected);
    }

    // Handles changing an option on the dropdown
    private void OnDeviceSelected(int index)
    {
        if (index >= 0 && index < availableDevices.Count)
        {
            VivoxInputDevice selectedDevice = availableDevices[index];
            SetVivoxInputDeviceAsync(selectedDevice);
            Debug.Log("Device selected.");
        }
    }

    // Sets the input device to whatever was chosen
    async void SetVivoxInputDeviceAsync(VivoxInputDevice device)
    {
        await VivoxService.Instance.SetActiveInputDeviceAsync(device);
        Debug.Log("Set new input device.");
    }

    // Switches to voice testing mode
    void TestingEnabled()
    {
        if (inChannel)
        {
            LeaveChannel(channelName);
            LeaveChannel(radioChannelName);
            inChannel = false;
            JoinEchoChannelAsync();
            testingOn = true;
            Debug.Log("Successfully enabled Voice Testing Mode.");
        }
    }

    // Stops voice testing mode
    void TestingDisabled()
    {
        if (inChannel)
        {
            LeaveChannel(testChannelName);
            inChannel = false;
            Join3DChannelAsync();
            testingOn = false;
            Debug.Log("Successfully disabled Voice Testing Mode.");
        }
    }

    public void OnEchoToggleChanged(bool value)
    {
        Debug.Log("Voice Testing toggle changed.");
        if (value == true)
        {
            TestingEnabled();
        } else if (value == false)
        {
            TestingDisabled();
        }
    }

    public void OnMuteToggleChanged(bool value)
    {
        Debug.Log("PTT toggle changed.");
        if (value == true)
        {
            PttEnabled();
        } else if (value == false)
        {
            PttDisabled();
        }
    }

    void PttEnabled()
    {
        if (pttOn == false)
        {
            VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
            pttOn = true;
            Debug.Log("Push to Talk Enabled.");
        }
    }

    void PttDisabled()
    {
        if (pttOn == true)
        {
            VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, channelName);
            pttOn = false;
            Debug.Log("Push to Talk Disabled.");
        }
    }
}
