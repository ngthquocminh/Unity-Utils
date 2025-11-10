using UnityEngine;

public class InterfaceFieldAttribute : PropertyAttribute
{
    public System.Type InterfaceType { get; }

    public InterfaceFieldAttribute(System.Type interfaceType)
    {
        InterfaceType = interfaceType;
    }
}
