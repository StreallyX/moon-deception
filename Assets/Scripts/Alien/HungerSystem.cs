using UnityEngine;
using UnityEngine.UI;

public class HungerSystem : MonoBehaviour
{
    [Header("Hunger Settings")]
    public float maxHunger = 100f;
    public float currentHunger = 100f;
    public float hungerDecayRate = 2f;
    public float coffeeDecayMultiplier = 1.5f;
    public float coffeeRestoreAmount = 10f;
    public float eatRestoreAmount = 40f;
    
    [Header("UI")]
    public Slider hungerSlider;
    public Image sliderFill;
    public Gradient hungerGradient;
    
    private float currentDecayMultiplier = 1f;
    private float coffeeEffectTimer = 0f;
    private float coffeeEffectDuration = 10f;
    
    public bool IsDead => currentHunger <= 0;
    
    void Start()
    {
        currentHunger = maxHunger;
        
        if (GameUIManager.Instance != null)
        {
            hungerSlider = GameUIManager.Instance.GetHungerSlider();
            sliderFill = GameUIManager.Instance.GetHungerBarFill();
        }
        else
        {
            if (hungerSlider == null)
            {
                GameObject sliderObj = GameObject.Find("HungerBar");
                if (sliderObj != null)
                {
                    hungerSlider = sliderObj.GetComponent<Slider>();
                }
            }
            
            if (hungerSlider != null)
            {
                hungerSlider.maxValue = maxHunger;
                hungerSlider.value = currentHunger;
                
                if (sliderFill == null)
                {
                    sliderFill = hungerSlider.fillRect?.GetComponent<Image>();
                }
            }
        }
        
        if (hungerGradient == null)
        {
            hungerGradient = new Gradient();
            hungerGradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.red, 0f),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.green, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
        }

        UpdateUI();
        Debug.Log("[HungerSystem] Initialized");
    }
    
    void Update()
    {
        if (coffeeEffectTimer > 0)
        {
            coffeeEffectTimer -= Time.deltaTime;
            if (coffeeEffectTimer <= 0)
            {
                currentDecayMultiplier = 1f;
            }
        }
        
        currentHunger -= hungerDecayRate * currentDecayMultiplier * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);
        
        UpdateUI();
        
        if (IsDead)
        {
            OnHungerDepleted();
        }
    }
    
    void UpdateUI()
    {
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.UpdateHungerBar(currentHunger, maxHunger);
        }
        else
        {
            if (hungerSlider != null)
            {
                hungerSlider.value = currentHunger;
                
                if (sliderFill != null && hungerGradient != null)
                {
                    sliderFill.color = hungerGradient.Evaluate(currentHunger / maxHunger);
                }
            }
        }
    }
    
    public void DrinkCoffee()
    {
        currentHunger = Mathf.Min(currentHunger + coffeeRestoreAmount, maxHunger);
        currentDecayMultiplier = coffeeDecayMultiplier;
        coffeeEffectTimer = coffeeEffectDuration;
        Debug.Log($"[HungerSystem] Drank coffee. Hunger: {currentHunger}, Decay multiplier: {currentDecayMultiplier}");
    }
    
    public void Eat()
    {
        currentHunger = Mathf.Min(currentHunger + eatRestoreAmount, maxHunger);
        Debug.Log($"[HungerSystem] Ate target. Hunger: {currentHunger}");
    }
    
    void OnHungerDepleted()
    {
        Debug.Log("[HungerSystem] Alien starved to death!");
    }
}
