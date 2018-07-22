using UnityEngine;
using UnityEngine.UI;

namespace BeatThat.DependencyInjection.Example_DepenendencyInjection
{
    [RequireComponent(typeof(Button))]
    public class IncrementButton_Injected : MonoBehaviour
    {
        // Add the [Inject] attribute to properties 
        // to have them set with the service registered to the property's type
        [Inject] CounterService counter;

        void Start()
        {
            //Something needs to call DependencyInjection.InjectDependencies.
            //
            //One option is to call it in MonoBehaviour::Start...
            InjectDependencies.On(this);

            GetComponent<Button>().onClick.AddListener(this.OnClick);
        }

        public void OnClick()
        {
            this.counter.Increment();
        }


    }
}