using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Lightbulb : MonoBehaviour
{
    public Sprite on;
    public Sprite off;
    
    public Material onMaterial;
    public Material offMaterial;
    
    private SpriteRenderer spriteRenderer;
    private Light2D light;
    
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        light = GetComponentInChildren<Light2D>();
    }
    
    private void Update()
    {
        var powered = Level.I.IsPowered(transform.position);
        spriteRenderer.sprite = powered ? on : off;
        light.enabled = powered;
        spriteRenderer.material = powered ? onMaterial : offMaterial;
    }
}
