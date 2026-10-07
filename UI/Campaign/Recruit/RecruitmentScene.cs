using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Memori.Utilities;
using UnityEngine;

namespace TJ.Recruit
{
public class RecruitmentScene : MonoBehaviour
{
    [SerializeField] private Camera recruitmentCamera1, recruitmentCamera2, recruitmentCamera3;
    [SerializeField] private Light recruitmentLight1, recruitmentLight2, recruitmentLight3;
    [SerializeField] private Light recruitmentBlueLight1, recruitmentBlueLight2, recruitmentBlueLight3;
    [SerializeField] private RenderTexture recruitmentRenderTexture1, recruitmentRenderTexture2, recruitmentRenderTexture3;
    public RenderTexture RecruitmentRenderTexture1 => recruitmentRenderTexture1;
    public RenderTexture RecruitmentRenderTexture2 => recruitmentRenderTexture2;
    public RenderTexture RecruitmentRenderTexture3 => recruitmentRenderTexture3;
    [SerializeField] private Transform recruitmentPrefabHolder1, recruitmentPrefabHolder2, recruitmentPrefabHolder3;
    GameObject[] gameObjects = new GameObject[3];
    string[] _loadedKeys = new string[3];
    CancellationTokenSource _setupCts;
    private void Awake()
    {
        ShutDown();
    }
    public async void SetUp(UnitName[] _recruitmentOptions)
    {
        _setupCts?.Cancel();
        _setupCts = new CancellationTokenSource();
        var token = _setupCts.Token;

        recruitmentCamera1.enabled = _recruitmentOptions.Length > 0;
        recruitmentCamera2.enabled = _recruitmentOptions.Length > 1;
        recruitmentCamera3.enabled = _recruitmentOptions.Length > 2;
        recruitmentLight1.enabled = _recruitmentOptions.Length > 0;
        recruitmentLight2.enabled = _recruitmentOptions.Length > 1;
        recruitmentLight3.enabled = _recruitmentOptions.Length > 2;
        recruitmentBlueLight1.enabled = _recruitmentOptions.Length > 0;
        recruitmentBlueLight2.enabled = _recruitmentOptions.Length > 1;
        recruitmentBlueLight3.enabled = _recruitmentOptions.Length > 2;
        DestroyAndReleaseAll();

        for (int i = 0; i < _recruitmentOptions.Length; i++)
        {
            _loadedKeys[i] = TabletopTavernData.Instance.GetRecruitmentPrefabKey(_recruitmentOptions[i]);
            GameObject prefab = await TabletopTavernData.Instance.LoadRecruitmentPrefabAsync(_recruitmentOptions[i]);
            if (token.IsCancellationRequested) return;
            if (prefab == null) return;
            gameObjects[i] = ModUnitPreview.Instantiate(_recruitmentOptions[i], prefab, GetHolder(i));
        }
    }
    private Transform GetHolder(int _index)
    {
        return _index switch
        {
            0 => recruitmentPrefabHolder1,
            1 => recruitmentPrefabHolder2,
            2 => recruitmentPrefabHolder3,
            _ => null,
        };
    }
    public void ShutDown()
    {
        _setupCts?.Cancel();
        recruitmentCamera1.enabled = false;
        recruitmentCamera2.enabled = false;
        recruitmentCamera3.enabled = false;
        recruitmentLight1.enabled = false;
        recruitmentLight2.enabled = false;
        recruitmentLight3.enabled = false;
        recruitmentBlueLight1.enabled = false;
        recruitmentBlueLight2.enabled = false;
        recruitmentBlueLight3.enabled = false;
        DestroyAndReleaseAll();
    }
    private void DestroyAndReleaseAll()
    {
        for (int i = 0; i < gameObjects.Length; i++)
        {
            if (gameObjects[i] != null)
            {
                Destroy(gameObjects[i]);
                gameObjects[i] = null;
            }
            if (_loadedKeys[i] != null)
            {
                // Object.Destroy is deferred to end-of-frame; releasing the Addressables
                // handle immediately can free the mesh/material data out from under the
                // still-alive instance and crash the engine. Wait a frame so the destroy
                // has actually taken effect first.
                StartCoroutine(ReleaseNextFrame(_loadedKeys[i]));
                _loadedKeys[i] = null;
            }
        }
    }
    private IEnumerator ReleaseNextFrame(string _key)
    {
        yield return null;
        AddressablesManager.Instance.Release(_key);
    }
}
}