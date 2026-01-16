using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// Custom NetworkTransform that allows the OWNER to control position.
/// This is needed because default NetworkTransform is Server Authoritative,
/// which means clients can't move their own characters.
///
/// If ClientNetworkTransform exists in your Netcode version, use that instead.
/// This is a fallback for older versions.
/// </summary>
public class OwnerNetworkTransform : NetworkTransform
{
    /// <summary>
    /// Override to allow owner to move their own object.
    /// By default, NetworkTransform only allows server to update.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        // Return FALSE = Owner has authority (client can control their own character)
        // Return TRUE = Server has authority (only server can move objects)
        return false;
    }
}
