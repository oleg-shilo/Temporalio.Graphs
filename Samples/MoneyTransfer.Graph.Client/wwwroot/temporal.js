
var wfId = "";
var runId = "";
var selectedWf = undefined;
var pollingDelay = 1000;
var waiting = false;
var executions = [];
var wfEvents = [];

function retrieveWfInfo() {
    fetch('/api/workflows')
        .then(response => response.json()) // Parse JSON response
        .then(data => {

            let dashboardUrl = `(<a href="http://localhost:8233/namespaces/default/workflows/${wfId}/${runId}/history" target="_blank">dashboard</a>)`;
            if (wfId === "" || runId === "") {
                dashboardUrl = "";
            }
            document.getElementById('wfUrl').innerHTML = dashboardUrl;

            getWfEvents();
        })
        .catch(error => {
            // Handle errors
            console.error('Error fetching data:', error);
        });
}

function getStatus() {
    fetch(`/api/status`)
        .then(response => response.json())
        .then(data => {
            // console.log(data);

            if (data?.serverAvailable === true)
                waiting = false;

            let serverStatus = (data?.serverAvailable !== true) ? (waiting ? "waiting..." : "not running") : "running";
            let workerStatus = (data?.workerAvailable !== true) ? "not running" : "running";

            document.getElementById('serverStatus').innerHTML = `${serverStatus}`;
            document.getElementById('workerStatus').innerHTML = `${workerStatus}`;

            let selectedWFStatus = "";

            executions = data?.executions;
            let pending = "";
            let inprogress = "";
            let processed = "";

            for (let i = 0; i < executions.length; i++) {
                let e = executions[i];
                let id = e.workflow_id.length > 17 ? e.workflow_id.substring(0, 21) + "..." : e.workflow_id;
                let itemHtml = `<option onclick="selectWorkflow('${e.workflow_id}')">${id}</option>`;

                if (e.completion_status === 'pending')
                    pending += itemHtml;
                else if (e.completion_status === 'inprogress')
                    inprogress += itemHtml;
                else if (e.completion_status === 'processed')
                    processed += itemHtml;

                if (wfId === e.workflow_id) {
                    selectedWf = e;
                    selectedWFStatus = e.completion_status;
                }
            }

            document.getElementById('pendingQueue').innerHTML = (pending === "" ? "<option>&nbsp</option>" : pending);
            document.getElementById('inprogressQueue').innerHTML = (inprogress === "" ? "<option>&nbsp</option>" : inprogress);
            document.getElementById('processedQueue').innerHTML = (processed === "" ? "<option>&nbsp</option>" : processed);

            if (selectedWFStatus.length > 0)
                document.getElementById('wfStatus').innerHTML = selectedWFStatus;

            retrieveWfInfo();

            setTimeout(getStatus, pollingDelay);
        });
}

function startServer() {
    fetch(`/api/start-server`);
    waiting = true;
    document.getElementById('serverStatus').innerHTML = `...`;
}

function stopServer() {
    fetch(`/api/stop-server`);
    document.getElementById('serverStatus').innerHTML = `...`;
}

function startWorkflow() {
    fetch(`/api/start-wf`);
}

function startWorkflowGraph() {
    fetch(`/api/start-wf-graph`);
}

function startWorker() {
    fetch(`/api/start-worker`);
    document.getElementById('workerStatus').innerHTML = `...`;
}

function stopWorker() {
    fetch(`/api/stop-worker`);
    document.getElementById('workerStatus').innerHTML = `...`;
}

function selectWorkflow(arg) {
    // alert("Selected: " + arg);
    let execution = executions.find(e => e.workflow_id === arg);
    if (execution) {
        wfId = execution.workflow_id;
        runId = execution.run_id;

        document.getElementById('wfId').innerHTML = wfId;
        document.getElementById('wfStatus').innerHTML = execution.completion_status;
        window.setMermaidSelectedStep(""); // unselect
        wfEvents = [];
        retrieveWfInfo();
    }
}

function getWfEvents() {
    if (wfId === "") {
        return;
    }

    fetch(`/api/workflows/${wfId}/runs/${runId}`)
        .then(response => response.json())
        .then(data => {
            // console.log(data);
            let eventsInfo = `<table>
                <tr >
                    <th>Event</th>
                    <th>Activity</th>
                    <th>Name</th>
                    <th>Started</th>
                    <th>Completed</th>
                    <th>Context</th>
                </tr>`;

            let activeStepID = "";
            wfEvents = data.events;
            for (let index = 0; data.events && index < data.events.length; index++) {
                const event = data.events[index];

                if (index === 0
                    && (event.eventType == "WorkflowExecutionStarted" || event.eventType == "EVENT_TYPE_WORKFLOW_EXECUTION_STARTED")) {
                    let attr = event.workflowExecutionStartedEventAttributes;
                    if (attr.input.payloads.length > 0) {
                        var inputData = decodeURIComponent(atob(attr.input.payloads[0].data));
                        document.getElementById('wfInput').value = inputData;
                        document.getElementById('wfResult').innerHTML = selectedWf?.result ? selectedWf.result : "";

                    }
                }

                // CLI vs grpc; the constants are encoded somewhat differently
                if (event.eventType == "ActivityTaskScheduled" || event.eventType == "EVENT_TYPE_ACTIVITY_TASK_SCHEDULED") {

                    let attr = event.activityTaskScheduledEventAttributes;

                    let activityContext = "";
                    let payloads = attr.input.payloads;
                    if (attr.input.payloads.length > 3) {

                        let id = atob(payloads[2].data);
                        let decodedName = atob(payloads[1].data);
                        decodedName = decodeURIComponent(JSON.parse(decodedName));
                        activityContext = decodedName;
                    }

                    let startDate = new Date(event.eventTime).toLocaleString();
                    let endDate = "";

                    let startedActivity = data.events.find(e =>
                        e.eventType == "EVENT_TYPE_ACTIVITY_TASK_STARTED" &&
                        e.activityTaskStartedEventAttributes?.scheduledEventId === event.eventId);

                    if (startedActivity)
                        startDate = new Date(startedActivity.eventTime).toLocaleString();

                    let endActivity = data.events.find(e =>
                        e.eventType == "EVENT_TYPE_ACTIVITY_TASK_COMPLETED" &&
                        e.activityTaskCompletedEventAttributes?.scheduledEventId === event.eventId);

                    if (endActivity) {
                        endDate = new Date(endActivity.eventTime).toLocaleString();
                        if (attr.activityType.name === "MakeDecision") {
                            try {
                                activityContext += " : " + atob(endActivity.activityTaskCompletedEventAttributes?.result?.payloads[0].data);
                            } catch (error) {
                            }
                        }
                    }
                    if (startDate.length > 0 && endDate.length === 0) {
                        activeStepID = attr.activityType.name;
                    }

                    eventsInfo += `
                    <tr>      
                    <td> ${event.eventId} </td>
                    <td> ${attr.activityId}</td> 
                    <td> ${attr.activityType.name} </td>
                    <td> ${startDate} </td>
                    <td> ${endDate} </td>
                    <td> ${activityContext} </td>
                    </tr>`;

                }
            }

            eventsInfo += `</table>`;

            let execution = executions.find(e => e.workflow_id === wfId);
            if (execution?.completion_status === "processed")
                activeStepID = "e"; // end node

            window.setMermaidActiveStep(activeStepID);
            document.getElementById('currStep').innerHTML = activeStepID == 'e' ? "exit point" : activeStepID == 's' ? "entry point" : activeStepID;
            document.getElementById('history').innerHTML = eventsInfo;

        })
        .catch(error => {
            console.error('Error fetching data:', error);
        });
}

function setSelectedStepInfo(activityId, textContent, nodesInfo) {

    const infoTag = document.querySelector('#stepInfo');
    let eventData = { started: "", ended: "", eventId: "", status: "not executed", type: "activity", result: "..." };

    if (wfEvents) {
        let event = wfEvents.find(e => e.activityTaskScheduledEventAttributes?.activityType.name === activityId);
        if (!event) {
            event = wfEvents.find(e => {
                if (e.activityTaskScheduledEventAttributes?.activityType.name === "MakeDecision") {
                    let decodedName = "";
                    let result = "";
                    let decisionId = "";

                    if (e.activityTaskScheduledEventAttributes?.input?.payloads.length > 3) {
                        result = atob(e.activityTaskScheduledEventAttributes?.input?.payloads[0].data);
                        decodedName = atob(e.activityTaskScheduledEventAttributes?.input?.payloads[1].data);
                        decisionId = atob(e.activityTaskScheduledEventAttributes?.input?.payloads[2].data);
                    }

                    if (decisionId == activityId) {
                        eventData.type = "decision";
                        eventData.result = result;
                        return true;
                    }
                    else {
                        return false;
                    }
                }
                else
                    return false;
            });
        }

        if (event) {

            let endActivity = wfEvents.find(e1 =>
                e1.eventType == "EVENT_TYPE_ACTIVITY_TASK_COMPLETED" &&
                e1.activityTaskCompletedEventAttributes?.scheduledEventId === event.eventId);
            if (endActivity) {
                eventData.ended = new Date(endActivity.eventTime).toLocaleString();
            }

            eventData.started = new Date(event.eventTime).toLocaleString();
            eventData.eventId = event.eventId;

            eventData.status = (event.activityTaskScheduledEventAttributes.workflowTaskCompletedEventId.toString() === undefined ? "In-progress" : "Competed");
        }
    }

    let nodeDefinition = nodesInfo.find(n => n.includes(activityId));
    if (nodeDefinition?.includes("WaitCondition"))
        eventData.type = "wait condition"

    if (eventData.type == "wait condition") {
        infoTag.innerHTML =
            `<b>Name:</b> ${textContent}<br>
             <b>Type:</b> ${eventData.type}<br>`;
    }
    else if (eventData.type == "decision") {
        infoTag.innerHTML =
            `<b>Name:</b> ${textContent}<br>
             <b>Type:</b> ${eventData.type}<br>
             <b>Result:</b> ${eventData.result}<br>
             <b>EventId:</b> ${eventData.eventId}<br>
             <b>Started:</b> ${eventData.started}<br>
             <b>Ended:</b> ${eventData.ended}<br>
             <b>Status:</b> ${eventData.status}`;
    }
    else if (activityId === "s") { // dedicated node with a well-known id
        infoTag.innerHTML =
            `<b>Name:</b> ${textContent}<br>
                <b>Type:</b> entry point`;
    }
    else if (activityId === "e") { // dedicated node with a well-known id
        infoTag.innerHTML =
            `<b>Name:</b> ${textContent}<br>
                <b>Type:</b> exit point<br>
                <b>Time:</b> ${selectedWf?.close_time ? new Date(selectedWf?.close_time).toLocaleString() : ""}`;
    }
    else {// normal activity
        infoTag.innerHTML =
            `<b>Name:</b> ${textContent}<br>
             <b>Type:</b> ${eventData.type}<br>
             <b>EventId:</b> ${eventData.eventId}<br>
             <b>Started:</b> ${eventData.started}<br>
             <b>Ended:</b> ${eventData.ended}<br>
             <b>Status:</b> ${eventData.status}`;
    }
}

// ==============================================
document.addEventListener('DOMContentLoaded', function () {
    setTimeout(getStatus, 200);
    setTimeout(startServer, 200);
});

window.setSelectedStepInfo = setSelectedStepInfo;