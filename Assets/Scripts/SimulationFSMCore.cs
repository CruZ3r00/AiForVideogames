using System.Collections.Generic;

namespace AcademicFlockingSimulation
{
    // Direct adaptation of the professor's FSM base structure, namespaced for project use.
    public delegate bool FSMCondition();
    public delegate void FSMAction();

    public class FSMTransition
    {
        public FSMCondition myCondition;

        private readonly List<FSMAction> myActions = new List<FSMAction>();

        public FSMTransition(FSMCondition condition, FSMAction[] actions = null)
        {
            myCondition = condition;
            if (actions != null)
            {
                myActions.AddRange(actions);
            }
        }

        public void Fire()
        {
            for (int i = 0; i < myActions.Count; i++)
            {
                myActions[i]();
            }
        }
    }

    public class FSMState
    {
        public readonly List<FSMAction> enterActions = new List<FSMAction>();
        public readonly List<FSMAction> stayActions = new List<FSMAction>();
        public readonly List<FSMAction> exitActions = new List<FSMAction>();

        private readonly Dictionary<FSMTransition, FSMState> links = new Dictionary<FSMTransition, FSMState>();

        public void AddTransition(FSMTransition transition, FSMState target)
        {
            links[transition] = target;
        }

        public FSMTransition VerifyTransitions()
        {
            foreach (FSMTransition transition in links.Keys)
            {
                if (transition.myCondition())
                {
                    return transition;
                }
            }

            return null;
        }

        public FSMState NextState(FSMTransition transition)
        {
            return links[transition];
        }

        public void Enter()
        {
            for (int i = 0; i < enterActions.Count; i++)
            {
                enterActions[i]();
            }
        }

        public void Stay()
        {
            for (int i = 0; i < stayActions.Count; i++)
            {
                stayActions[i]();
            }
        }

        public void Exit()
        {
            for (int i = 0; i < exitActions.Count; i++)
            {
                exitActions[i]();
            }
        }
    }

    public class FSM
    {
        public FSMState current;

        public FSM(FSMState state)
        {
            current = state;
            current.Enter();
        }

        public void Update()
        {
            FSMTransition transition = current.VerifyTransitions();
            if (transition != null)
            {
                current.Exit();
                transition.Fire();
                current = current.NextState(transition);
                current.Enter();
            }
            else
            {
                current.Stay();
            }
        }
    }
}
