using ModApi.GameLoop;
using UnityEngine;
using Assets.Scripts;
using ModApi.GameLoop.Interfaces;

namespace FlarePath
{

       public class FlarePathUserInterface : MonoBehaviourBase, IFlightFixedUpdate, IFlightUpdate
       {
              public static FlarePathUserInterface Instance { get; private set; }

              void Awake()
              {
                     Instance = this;
              }

              public void Update()
              {

              }

              public void FlightFixedUpdate(in FlightFrameData flightFrameData)
              {
              }

              public void FlightUpdate(in FlightFrameData flightFrameData)
              {

              }

       }
}