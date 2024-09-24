mermaid.initialize({
    startOnLoad: true,
    flowchart: { useMaxWidth: true, htmlLabels: true, curve: 'cardinal' },
    securityLevel: 'loose'
});

eleM = document.querySelector('.mermaid');
eleE = document.querySelector('#err');

setTimeout(mermaidDraw, 200);

async function mermaidDraw() {
    try {
        graphDefinition = await getMermaidDefinition();
        const {
            svg
        } = await mermaid.render('graphDiv', graphDefinition);
        eleM.innerHTML = svg;

        setTimeout(addClickHandlers, 200);

    } catch (err) {
        if (err instanceof ReferenceError) {
            varname = err.message.split(' ')[0];
            window[varname] = varname;
            setTimeout(mermaidDraw, 0);
        }
        console.error(err);
        eleE.insertAdjacentHTML('beforeend', `🚫${err.message}\n`);
    }
};

async function getMermaidDefinition() {
    let text = `
flowchart LR
s((In)) --> Withdraw
Withdraw --> -1485020306{Currency != 'AUD'}
-1485020306{Currency != 'AUD'} -- yes --> ConvertCurrency[Convert Currency]
ConvertCurrency[Convert Currency] --> 78577353{Is TFN Known}
78577353{Is TFN Known} -- yes --> NotifyAto[Notify Ato]
NotifyAto[Notify Ato] --> Deposit
Deposit --> 1546202137{{Interpol Check}}
1546202137{{Interpol Check}} -- Signaled --> Refund
Refund --> NotifyPolice[Notify Police]
NotifyPolice[Notify Police] --> e((Out))
78577353{Is TFN Known} -- no --> TakeNonResidentTax[Take Non Resident Tax]
TakeNonResidentTax[Take Non Resident Tax] --> Deposit
-1485020306{Currency != 'AUD'} -- no --> 78577353{Is TFN Known}
1546202137{{Interpol Check}} -- Timeout --> e((Out))
`;
    // use this to check all mermaid node ids
    // let allNodes = text
    //     .split(/\r?\n/)
    //     .flatMap(x => x.split("-->"))
    //     .map(x => x.split(" -- ")[0]
    //         .split("[")[0]
    //         .split("(")[0]
    //         .trim())
    //     .map(x => {
    //         if (x.includes("{{"))
    //             return x.split("{{")[0] + ":WaitCondition";
    //         else if (x.includes("{"))
    //             return x.split("{")[0] + ":MakeDecision";
    //         else
    //             return x;
    //     })
    //     .filter(x => !x.includes("flowchart") && x.length > 0);
    // allNodes = Array.from(new Set(allNodes));
    // console.log(allNodes);
    text = text + `\n\nclassDef activeStep fill:#e94,stroke-width:1px;\nclassDef selectedStep stroke:#f96,stroke-width:2px;`;
    return text;
}

function toMermaidId(nodeText) {
    return nodeText
        .split("[")[0]
        .split("(")[0]
        .split("-")[0]
        .trim();
}

function setNodeUniqueClass(node, className) {
    const nodes = document.querySelectorAll('g.node');
    nodes.forEach(nd => {
        nd.classList.remove(className);
        if (nd === node) {
            node.classList.add(className);
        }
    });
}

function setMermaidSelectedStep(name) {
    if (name === null || name === undefined || name === "") {
        name = "<unknown step>"; // use any string to ensure mismatch with any mermaid node
        document.querySelector('#stepInfo').innerHTML = "";
    }
    const nodes = document.querySelectorAll('g.node');
    nodes.forEach(nd => {
        if (nd.id.includes(name))
            nd.classList.add('selectedStep');

        else
            nd.classList.remove('selectedStep');
    });
}

function setMermaidActiveStep(name) {

    if (name === null || name === undefined || name === "") {
        name = "<unknown step>"; // use any string to ensure mismatch with any mermaid node
    }

    const nodes = document.querySelectorAll('g.node');

    for (let i = 0; i < nodes.length; i++) {
        if (nodes[i].id.includes(name))
            nodes[i].classList.add("activeStep");
        else
            nodes[i].classList.remove("activeStep");
    }
}

function isStarted(node) {
    const nodes = document.querySelectorAll('g.node');

    let found = -1;
    for (let i = 0; i < nodes.length; i++) {
        if (nodes[i].matches('.activeStep')) {
            found = i;
            if (nodes[i] === node) {
                return { started: true, finished: false };
            }
            break;
        }
    }

    if (found !== -1) {
        if (Array.from(nodes).slice(0, found).includes(node))
            return { started: true, finished: true };
        else
            return { started: false, finished: false };
    }

    return false;
}

function handleClick(node, parentDataId) {

    setNodeUniqueClass(node, "selectedStep");

    const info = document.querySelector('#stepInfo');
    parentDataId = toMermaidId(parentDataId);
    let status = "<pending>";
    let runId = generateRandomUUID();
    let start = "...";
    let end = "...";
    let duration = "...";

    let stepDetails = isStarted(node);
    if (stepDetails.started) {
        status = stepDetails.finished ? "completed" : "In progress";

        const now = new Date(); // Get the current date and time
        const tenMinutesAgo = new Date(now.getTime() - 10 * 60 * 1000);
        start = tenMinutesAgo.toLocaleString();
        if (stepDetails.finished) {
            end = tenMinutesAgo.toLocaleString();
            duration = "45 sec";
        }
    }
    info.innerHTML = `Id: '${parentDataId}'<br>Status: ${status}<br>RunId: ${runId}<br>Started: ${start}<br>Completed: ${end}<br>Duration: ${duration}`;

}


function addClickHandlers() {

    // it's possible to use `click nodeId callback " "` directly in the mermaid definition but it will means parsing the 
    // definition, which only mermaid should do
    const nodes = document.querySelectorAll('g.node');
    nodes.forEach(node => {
        node.style.cursor = 'pointer';
        node.addEventListener('click', (event) => {
            const clickedNode = event.target.closest('g'); // Get the closest <g> element (the node)
            clickedNode.setAttribute("cursor", "hand");
            const nodeId = clickedNode.id;
            const parentNode = clickedNode.parentElement;
            const parentDataId = parentNode.getAttribute('data-id') || 'No data-id attribute';
            handleClick(node, parentDataId);
        });
    });
}

function generateRandomUUID() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'
        .replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
}

window.setMermaidActiveStep = setMermaidActiveStep;
window.setMermaidSelectedStep = setMermaidSelectedStep;