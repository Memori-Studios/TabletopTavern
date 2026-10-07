using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using System.Collections;

namespace TJ
{
    public class ArtilleryCrewMemberGO : MonoBehaviour
    {
        [SerializeField] private Animator _crewAnimator;
        public Animator CrewAnimator => _crewAnimator;
        [SerializeField] private GameObject meleeWeaponGO;
        public void PlayAnimation(string animationName)
        {
            _crewAnimator.Play(animationName);
        }
        public void PlayAnimationWithRandomDelay(string animationName)
        {
            float delay = Random.Range(0f, 0.75f);
            StartCoroutine(PlayAnimationWithDelay(animationName, delay));
        }
        public IEnumerator PlayAnimationWithDelay(string animationName, float delay)
        {
            yield return new WaitForSeconds(delay);
            _crewAnimator.Play(animationName);
        }
        public void SetMeleeMode(bool isActive)
        {
            if (meleeWeaponGO != null)
            {
                meleeWeaponGO.SetActive(isActive);
            }
            _crewAnimator.SetBool("meleeMode", isActive);
        }
    }
}