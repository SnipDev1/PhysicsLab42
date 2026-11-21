using UnityEngine;

public class DigitController : MonoBehaviour
{
    [Header("Сегменты (Палочки)")]
    public GameObject segA;
    public GameObject segB;
    public GameObject segC;
    public GameObject segD;
    public GameObject segE;
    public GameObject segF;
    public GameObject segG;
    public GameObject dot; // Точка (если есть)

    // Таблица истинности для 7 сегментов (A-G)
    private readonly bool[,] table = {
        {true, true, true, true, true, true, false},  // 0
        {false, true, true, false, false, false, false}, // 1
        {true, true, false, true, true, false, true}, // 2
        {true, true, true, true, false, false, true}, // 3
        {false, true, true, false, false, true, true}, // 4
        {true, false, true, true, false, true, true}, // 5
        {true, false, true, true, true, true, true},  // 6
        {true, true, true, false, false, false, false}, // 7
        {true, true, true, true, true, true, true},  // 8
        {true, true, true, true, false, true, true}   // 9
    };

    public void SetDigit(int n, bool showDot = false) {
        if (n < 0 || n > 9) { SetPower(false); return; }
        
        if(segA) segA.SetActive(table[n, 0]);
        if(segB) segB.SetActive(table[n, 1]);
        if(segC) segC.SetActive(table[n, 2]);
        if(segD) segD.SetActive(table[n, 3]);
        if(segE) segE.SetActive(table[n, 4]);
        if(segF) segF.SetActive(table[n, 5]);
        if(segG) segG.SetActive(table[n, 6]);
        if(dot) dot.SetActive(showDot);
    }

    public void SetPower(bool on) {
        if(segA) segA.SetActive(false); if(segB) segB.SetActive(false);
        if(segC) segC.SetActive(false); if(segD) segD.SetActive(false);
        if(segE) segE.SetActive(false); if(segF) segF.SetActive(false);
        if(segG) segG.SetActive(false); if(dot) dot.SetActive(false);
    }
}