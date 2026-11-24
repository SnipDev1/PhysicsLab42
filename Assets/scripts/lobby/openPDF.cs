using UnityEngine;
using System.IO;

public class OpenPDF : MonoBehaviour
{
    public string pdfFileName = "labs.pdf";

    public void OpenInstruction() => OpenLab42Pdf(pdfFileName);

    public static void OpenLab42Pdf(string fileName = "labs.pdf")
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            Application.OpenURL(Application.streamingAssetsPath + "/" + fileName);
            return;
        }

        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(filePath))
            Application.OpenURL("file:///" + filePath.Replace('\\', '/').TrimStart('/'));
    }
}
