using UnityEngine;

namespace Ransom
{
    /// <summary>
    /// A <see cref="ScriptableObject"/> asset that acts as the template (prefab) for
    /// pooled <see cref="Timer"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create an asset via the Unity menu:
    /// <b>Assets → Create → Ransom → ScriptableObject → Manager → TimerTemplate</b>.
    /// Assign it to the <see cref="TimerManager"/> Inspector field. The serialized
    /// <see cref="PoolableObjectTemplate{T}.Source"/> field exposes all of
    /// <see cref="Timer"/>'s <c>[SerializeField]</c> fields directly in the Inspector,
    /// giving designers control over pool-wide defaults (e.g. loop flag, unscaled
    /// time) without touching code.
    /// </para>
    /// <para>
    /// When <see cref="ClassObjectPool{T}"/> allocates a new <see cref="Timer"/>, it
    /// calls <see cref="Timer.Create(Timer)"/> with <c>Source</c> as the argument,
    /// copying those defaults into the fresh instance before it is handed to the
    /// caller.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "TimerTemplate",
        menuName = "Ransom/ScriptableObject/Prefab/Timer",
        order    = 1
    )]
    public sealed class TimerTemplate : PoolableObjectTemplate<Timer>
    {
    }
}