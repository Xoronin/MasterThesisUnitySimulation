// Assets/Tests/ScatterMathProbe.cs
using UnityEngine;

public class ScatterMathProbe : MonoBehaviour
{
    void Start()
    {
        float fMHz = 2000f;
        float λ = 299792458f / (fMHz * 1e6f);
        float k0 = 2f * Mathf.PI / λ;

        float[] sigma = { 0f, 0.02f, 0.05f, 0.1f };
        float[] S = { 0.1f, 0.3f, 0.6f };
        float rhoSmooth = 0.7f;
        float sinψ = Mathf.Sin(30f * Mathf.Deg2Rad);

        Debug.Log("==== Diffuse scattering parameter sweep ====");
        foreach (var s in S)
        {
            foreach (var sig in sigma)
            {
                float rhoRough = rhoSmooth * Mathf.Exp(-2f * Mathf.Pow(k0 * sig * sinψ, 2f));
                float lobe = Mathf.Cos(30f * Mathf.Deg2Rad) * Mathf.Pow(Mathf.Cos(30f * Mathf.Deg2Rad), 1);
                float M = s * (1f - rhoRough * rhoRough) * lobe;
                float loss = 10f - 20f * Mathf.Log10(Mathf.Max(M, 1e-6f)); // scatterBaseLossDb = 10
                Debug.Log($"S={s:F2}, σ={sig:F3} → ρ_rough={rhoRough:F3}, M={M:E3}, Loss={loss:F1} dB");
            }
        }
    }
}
