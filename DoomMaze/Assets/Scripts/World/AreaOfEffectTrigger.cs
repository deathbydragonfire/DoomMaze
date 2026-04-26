using UnityEngine;
using static Beautify.Universal.Beautify;
using static UnityEngine.UI.GridLayoutGroup;

/// <summary>
/// Runtime trigger that asks its owning area of effect to handle what happens when a player .
/// </summary>
public class AreaOfEffectTrigger : MonoBehaviour
{
    private EnemyAreaOfEffect _areaOfEffect;

    public void Configure(EnemyAreaOfEffect areaOfEffect)
    {
        _areaOfEffect = areaOfEffect;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _areaOfEffect?.HandleEnterAreaOfEffect(other);
    }
}
