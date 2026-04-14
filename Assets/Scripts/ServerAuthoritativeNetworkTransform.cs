using Unity.Netcode.Components;

public class ServerAuthoritativeNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return true;
    }
}
