using UnityEngine;
using TMPro;

public class ScoreRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;  // Ej: "#1"
    [SerializeField] private TMP_Text nameText;  // Ej: "DEV"
    [SerializeField] private TMP_Text scoreText; // Ej: "5000"

    public void Setup(int rank, string playerName, int score)
    {
        rankText.text = "#" + rank;
        nameText.text = playerName;
        scoreText.text = score.ToString("N0"); // Formatea con separador de miles (ej: 5.000)
    }
}