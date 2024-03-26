using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Prefabulous.VRC.Editor;
using UnityEditor.Animations;
using UnityEngine;

[assembly: ExportsPlugin(typeof(PrefabulousAnimatorAlterationPlugin))]
namespace Prefabulous.VRC.Editor
{
    public class PrefabulousAnimatorAlterationPlugin : Plugin<PrefabulousAnimatorAlterationPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run("Alterate Animators", Alterate);
        }

        private void Alterate(BuildContext ctx)
        {
            var animationClipToNullableModification = new Dictionary<AnimationClip, AnimationClip>();
            ctx.AvatarDescriptor.baseAnimationLayers
                .Where(layer => !layer.isDefault && layer.animatorController != null);
                //.?????
            // TODO ????????????????????????????????????
            // TODO ????????????????????????????????????
            // TODO ????????????????????????????????????
            // TODO ????????????????????????????????????
            // TODO ????????????????????????????????????
            // TODO ????????????????????????????????????
            // TODO ????????????????????????????????????
            // TODO ????????????????????????????????????
            
            foreach (var layer in ctx.AvatarDescriptor.baseAnimationLayers)
            {
                if (!layer.isDefault)
                {
                    Visit(layer.animatorController, animationClipToNullableModification);
                }
            }
        }

        private void Visit(RuntimeAnimatorController animator, Dictionary<AnimationClip, AnimationClip> visitedAnimationClips)
        {
            var ctrl = (AnimatorController)animator;
            var allClips = FindAllClips(ctrl)
                .Where(clip => !visitedAnimationClips.ContainsKey(clip))
                .ToList();
            
            foreach (var originalClip in allClips)
            {
                AnimationClip modifiedClipNullable = null;
                TryModify(originalClip, ref modifiedClipNullable);
                visitedAnimationClips.Add(originalClip, modifiedClipNullable);
            }
        }

        private void TryModify(AnimationClip originalClip, ref AnimationClip modifiedNullable)
        {
            var instantiate = Object.Instantiate(originalClip);
            
            modifiedNullable = instantiate;
        }

        private AnimationClip[] FindAllClips(AnimatorController ctrl)
        {
            return ctrl.layers
                .SelectMany(AllStateMachinesOf)
                .SelectMany(sm => sm.states)
                .SelectMany(AllAnimationsOf)
                .Distinct()
                .ToArray();
        }

        private List<AnimatorStateMachine> AllStateMachinesOf(AnimatorControllerLayer layer)
        {
            var machines = new List<AnimatorStateMachine>();
            
            var sm = layer.stateMachine;
            machines.Add(sm);
            RecursivelyAddMachines(sm.stateMachines, machines);

            return machines;
        }

        private void RecursivelyAddMachines(ChildAnimatorStateMachine[] csms, List<AnimatorStateMachine> machines)
        {
            foreach (var csm in csms)
            {
                var sm = csm.stateMachine;
                machines.Add(sm);
                RecursivelyAddMachines(sm.stateMachines, machines);
            }
        }

        private List<AnimationClip> AllAnimationsOf(ChildAnimatorState state)
        {
            var clips = new List<AnimationClip>();

            switch (state.state.motion)
            {
                case AnimationClip clip:
                    clips.Add(clip);
                    break;
                case BlendTree bt:
                    RecursivelyAddClips(bt, clips);
                    break;
            }

            return clips;
        }

        private void RecursivelyAddClips(BlendTree blendTree, List<AnimationClip> clips)
        {
            foreach (var childMotion in blendTree.children)
            {
                switch (childMotion.motion)
                {
                    case AnimationClip clip:
                        clips.Add(clip);
                        break;
                    case BlendTree innerBlendTree:
                        RecursivelyAddClips(innerBlendTree, clips);
                        break;
                }
            }
        }
    }
}