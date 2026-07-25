using Memori.Audio;
using Memori.Steamworks;
using UnityEngine;
using UnityEngine.Events;

public class Sheep : MonoBehaviour
{
    [SerializeField] private int requiredClicks = 5;
    [SerializeField] private GameObject explosion;
    
    private int clickCount = 0;
    private SphereCollider sphereCollider; // Auto-reference for validation
    
    private void Awake()
    {
        // Ensure SphereCollider exists
        sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            Debug.LogError($"ClickReactor on {gameObject.name} requires a SphereCollider!");
            enabled = false;
            return;
        }
        
        // Optional: Make it a trigger if you want overlap detection too
        // sphereCollider.isTrigger = true;
    }
    
    private void OnMouseDown()
    {
        clickCount++;
        
        // Visual feedback (optional - add ParticleSystem or Animator if needed)
        transform.localScale = Vector3.one * 1.1f; // Brief scale pop
        Invoke(nameof(ResetScale), 0.1f);

        IAudioRequester.Instance.PlaySFX("sheep"); // Play sheep sound on click
        
        if (clickCount >= requiredClicks)
        {
            React();
        }
    }
    
    private void ResetScale()
    {
        transform.localScale = Vector3.one;
    }
    
    private void React()
    {
        explosion.SetActive(true);
        SteamAchievements.Unlock(AchievementId.SheepSlayer);
        IAudioRequester.Instance.PlaySFX("explosion");
        Destroy(gameObject);
    }
}