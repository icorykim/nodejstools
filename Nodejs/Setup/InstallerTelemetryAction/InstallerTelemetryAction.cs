﻿//*********************************************************//
//    Copyright (c) Microsoft. All rights reserved.
//    
//    Apache 2.0 License
//    
//    You may obtain a copy of the License at
//    http://www.apache.org/licenses/LICENSE-2.0
//    
//    Unless required by applicable law or agreed to in writing, software 
//    distributed under the License is distributed on an "AS IS" BASIS, 
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or 
//    implied. See the License for the specific language governing 
//    permissions and limitations under the License.
//
//*********************************************************//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Globalization;
using Microsoft.Deployment.WindowsInstaller;

namespace Microsoft.NodejsTools.Telemetry
{
    public class InstallerTelemetryActions {

        [CustomAction]
        public static ActionResult RecordInstallStartTime(Session session) {
            session["InstallStartTime"] = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult LogInstallSuccessResult(Session session) {
            session.Log("Begin Telemetry Log");
            LogInstallStatus("Success", session);
            session.Log("End Telemetry Log");
            return ActionResult.Success;
        }

        [CustomAction] 
        public static ActionResult LogInstallErrorResult(Session session) {
            session.Log("Begin Telemetry Log");
            LogInstallStatus("Error", session);
            session.Log("End Telemetry Log");
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult LogInstallCancelResult(Session session) {
            session.Log("Begin Telemetry Log");
            LogInstallStatus("Cancel", session);
            session.Log("End Telemetry Log");
            return ActionResult.Success;
        }

        private static void LogInstallStatus(string InstallStatus, Session session)
        {
            TimeSpan installTime = DateTime.Now - DateTime.Parse(session["InstallStartTime"]);
            bool isInstalled = session.EvaluateCondition("Installed");
            string currentState = null;
            string requestState = null;

            FeatureInfoCollection featureInfoCollection = session.Features;
            foreach (FeatureInfo featureInfo in featureInfoCollection)
            {
                currentState = featureInfo.CurrentState.ToString("F");
                requestState = featureInfo.RequestState.ToString("F");
                // we just want the current and requested state of A feature to understand if its a new user, upgrade, reinstall or remove.
                break;
            }
            session.Log("Starting POST");
            Dictionary<string, object> data = new Dictionary<string, object>() {
                {
                    "iKey", "377a3718-78a7-49df-abcc-1001317db729"
                }, {
                    "name", "Microsoft.ApplicationInsights.Event"
                }, {
                    "time", DateTime.Now.ToUniversalTime().ToString(CultureInfo.InvariantCulture)
                }, {
                    "data", new Dictionary <string, object> () {
                        {
                            "baseType", "EventData"
                        }, {
                            "baseData", new Dictionary <string, object> () {
                                {
                                    "ver", "2"
                                }, {
                                    "name", "NtvsInstallerTelemetry"
                                }, {
                                    "properties", new Dictionary <string, string> () {
                                        {
                                            "InstallStatus", InstallStatus
                                        }, {
                                            "IsNtvsInstalled", isInstalled.ToString(CultureInfo.InvariantCulture)
                                        }, {
                                            "CurrentState", currentState
                                        }, {
                                            "RequestState", requestState
                                        }, {
                                            "NtvsVersion", session["NtvsVersion"]
                                        }, {
                                            "VSVersion", session["VSVersion"]
                                        }, {
                                            "MsiVersion", session["MsiVersion"]
                                        }, {
                                            "TimeTakenInSeconds", installTime.TotalSeconds.ToString(CultureInfo.InvariantCulture)
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            string jsonString = (new JavaScriptSerializer()).Serialize(data);
            using (WebClient client = new WebClient())
            {
                string response = client.UploadString("https://dc.services.visualstudio.com/v2/track", jsonString);
                session.Log(response);
            }
        }

    }
}