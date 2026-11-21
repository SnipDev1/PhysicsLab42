using UnityEngine;

public class HyperLink : MonoBehaviour
{
    public void URL(string link) => Application.OpenURL(link);
}
