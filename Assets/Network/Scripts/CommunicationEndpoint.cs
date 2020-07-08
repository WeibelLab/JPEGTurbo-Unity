using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


[System.Serializable]
public class CommunicationJsonEvent : UnityEvent<JSONObject> { }
[System.Serializable]
public class CommunicationStringEvent : UnityEvent<string> { }
[System.Serializable]
public class CommunicationByteEvent : UnityEvent<byte[]> { }


[System.Serializable]
public enum CommunicationMessageType { Byte, String, Json }

[System.Serializable]
public enum CommunicationHeaderType { Length, Time }

[System.Serializable]
public struct CommunicationEndpoint
{

    public string Name;
    public string Address;
    public int Port;

    public CommunicationEndpoint(string Address, int Port, string Name = "device")
    {
        this.Name = Name;
        this.Address = Address;
        this.Port = Port;
    }

    public void SetAddress(string address)
    {
        this.Address = address;
    }
    public void SetPort(int port)
    {
        this.Port = port;
    }
    public override string ToString()
    {
        return string.Format("\"{0}\" ({1}:{2})",Name, Address, Port);
    }
}