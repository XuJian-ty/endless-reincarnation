using System.Collections;
using UnityEngine;

namespace ProjectBase
{
/// <summary>
/// 池对象的定时/立即回收代理。
/// 适合特效这类“生成后过一段时间回池”的对象，避免反复 Destroy。
/// </summary>
public sealed class PooledObjectReturner : MonoBehaviour
{
    private Coroutine _returnRoutine;
    private GameObject _sourcePrefab;
    private bool _returned;

    public void Bind(GameObject sourcePrefab)
    {
        _sourcePrefab = sourcePrefab;
        _returned = false;

        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
        }
    }

    public void ScheduleReturn(float delay)
    {
        if (_sourcePrefab == null)
            return;

        if (_returnRoutine != null)
            StopCoroutine(_returnRoutine);

        _returnRoutine = StartCoroutine(ReturnAfterDelay(Mathf.Max(0f, delay)));
    }

    public void ReturnNow()
    {
        if (this == null || _returned || _sourcePrefab == null)
            return;

        _returned = true;
        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
        }

        PoolMgr.GetInstance().PushObj(_sourcePrefab, gameObject);
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (this == null || _returned || _sourcePrefab == null)
            yield break;

        _returnRoutine = null;
        ReturnNow();
    }

    private void OnDisable()
    {
        _returnRoutine = null;
    }

    private void OnDestroy()
    {
        _returnRoutine = null;
    }
}
}
