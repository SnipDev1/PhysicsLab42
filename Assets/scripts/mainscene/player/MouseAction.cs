using UnityEngine;
using UnityEngine.Events;
public class MouseAction : MonoBehaviour
{
    public UnityEvent _down;
    public UnityEvent _enter;
     public UnityEvent _exit;
    public void InvokeDown() => _down?.Invoke();

    private void OnMouseEnter()
    {
        _enter?.Invoke();

    }

    private void OnMouseExit()
    {
        _exit?.Invoke();

    }
}
