using System.Collections;
using UnityEngine;

public class GhostTrail : MonoBehaviour
{
    public SpriteRenderer source;
    public float spawnInterval = 0.05f;
    public float lifeTime = 0.25f;
    public float initialAlpha = 0.35f;
    public int sortingOffset = -1;
    public bool active = true;

    private float timer = 0f;

    void Reset()
    {
        source = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (!active || source == null || source.sprite == null)
            return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnGhost();
        }
    }

    private void SpawnGhost()
    {
        var go = new GameObject("GhostSprite");
        var srGhost = go.AddComponent<SpriteRenderer>();
        srGhost.sprite = source.sprite;
        srGhost.flipX = source.flipX;
        srGhost.flipY = source.flipY;
        srGhost.color = new Color(1f, 1f, 1f, initialAlpha);
        srGhost.sortingLayerID = source.sortingLayerID;
        srGhost.sortingOrder = source.sortingOrder + sortingOffset;

        go.transform.position = transform.position;
        go.transform.rotation = transform.rotation;
        go.transform.localScale = transform.localScale;

        StartCoroutine(FadeAndDestroy(srGhost));
    }

    private IEnumerator FadeAndDestroy(SpriteRenderer srGhost)
    {
        float t = 0f;
        Color c = srGhost.color;
        while (t < lifeTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / lifeTime);
            srGhost.color = new Color(c.r, c.g, c.b, initialAlpha * k * k);
            yield return null;
        }
        if (srGhost != null)
            Destroy(srGhost.gameObject);
    }
}

