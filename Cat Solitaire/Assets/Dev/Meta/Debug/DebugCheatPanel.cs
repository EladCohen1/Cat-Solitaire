using UnityEngine;
using UnityEngine.SceneManagement;

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
