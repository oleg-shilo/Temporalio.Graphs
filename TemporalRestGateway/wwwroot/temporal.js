
var wfId = "";
var runId = "";
var pollingDelay = 1000;
var waiting = false;
var executions = [];

function retrieveWfInfo() {
    fetch('/api/workflows')
        .then(response => response.json()) // Parse JSON response
        .then(data => {

            let wfInfo = `
                <p> <b>WorkflowId:</b> ${wfId}<br>
                    <b>RunId:</b> ${runId}</p>
                    `;
            document.getElementById('result').innerHTML = wfInfo;

            retrieveWfEvents();
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

            executions = data?.executions;
            let pending = "";
            let inprogress = "";
            let processed = "";

            for (let i = 0; i < executions.length; i++) {
                let e = executions[i];
                let id = e.workflow_id.length > 17 ? e.workflow_id.substring(0, 17) + "..." : e.workflow_id;
                let itemHtml = `<option onclick="selectWorkflow('${e.workflow_id}')">${id}</option>`;

                if (e.completion_status === 'pending')
                    pending += itemHtml;
                else if (e.completion_status === 'inprogress')
                    inprogress += itemHtml;
                else if (e.completion_status === 'processed')
                    processed += itemHtml;
            }

            document.getElementById('pendingQueue').innerHTML = (pending === "" ? "<option>&nbsp</option>" : pending);
            document.getElementById('inprogressQueue').innerHTML = (inprogress === "" ? "<option>&nbsp</option>" : inprogress);
            document.getElementById('processedQueue').innerHTML = (processed === "" ? "<option>&nbsp</option>" : processed);

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

        let wfInfo = `
                <p> <b>WorkflowId:</b> ${wfId}<br>
                    <b>RunId:</b> ${runId}</p>
                    `;
        document.getElementById('result').innerHTML = wfInfo;
        retrieveWfInfo();
    }
}

function retrieveWfEvents() {
    if (wfId === "") {
        return;
    }

    fetch(`/api/workflows/${wfId}/runs/${runId}`)
        .then(response => response.json())
        .then(data => {
            // console.log(data);
            let eventsInfo = `<table>
                <tr>
                    <th>Event</th>
                    <th>Activity</th>
                    <th>Name</th>
                    <th>Started</th>
                    <th>Details</th>
                </tr>`;

            for (let index = 0; data.events && index < data.events.length; index++) {
                const event = data.events[index];

                if (index === 0
                    && (event.eventType == "WorkflowExecutionStarted" || event.eventType == "EVENT_TYPE_WORKFLOW_EXECUTION_STARTED")) {
                    let attr = event.workflowExecutionStartedEventAttributes;
                    if (attr.input.payloads.length > 0) {
                        var inputData = decodeURIComponent(atob(attr.input.payloads[0].data));
                        document.getElementById('wfInput').innerHTML = inputData;
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
                        activityContext = `Name: '${decodedName}'; &nbsp;&nbsp;&nbsp;Id: ${id}`;
                    }

                    eventsInfo += `
                                <tr>      
                                    <td> ${event.eventId} </td>
                                    <td> ${attr.activityId}</td> 
                                    <td> ${attr.activityType.name} </td>
                                    <td> ${new Date(event.eventTime).toLocaleString()} </td>
                                    <td> ${activityContext} </td>
                                    </tr>`;
                }
            }
            eventsInfo += `</table>`;

            document.getElementById('history').innerHTML = eventsInfo;

        })
        .catch(error => {
            console.error('Error fetching data:', error);
        });
}

document.addEventListener('DOMContentLoaded', function () {
    setTimeout(getStatus, 200);
    setTimeout(startServer, 200);
});
