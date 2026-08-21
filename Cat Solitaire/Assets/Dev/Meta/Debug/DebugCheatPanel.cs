using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-facing helper so you can exercise the hub before the solitaire level exists.
/// Wire the public methods to UI Buttons.
/// </summary>
public class DebugCheatPanel : MonoBehaviour
{
    [SerializeField] CurrencyAmount[] _grantOnClick;

    public void GrantCurrency()
    {
        GameBootstrap.Instance.Wallet.Grant(_grantOnClick);
    }

    public void ResetSave()
    {
        SaveService.Delete();
        Destroy(GameBootstrap.Instance.gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
