# Relativa User Guide

Relativa is a CRM platform for managing clients, deals, and sales. It is organized around organizations and workspaces: you first join an organization (a company or team), then enter a specific workspace where client and deal data is stored. This guide covers all the main scenarios for working with the platform.


## Key Concepts

Organization — the top level of the platform. It brings a team together and can have multiple workspaces.

Workspace — an isolated working area inside an organization (for example, a separate department or project). This is where entities live.

Entity — a business record: a client, deal, contract, and so on. Each entity has a set of properties and connections to other entities.

Graph — a visual map of your organization: who belongs to which workspaces, what entities exist in them, and how they are connected.

Role — a set of permissions that determines what you can do inside an organization or workspace.


---


## Scenario 1. Registration

Open the login page and click "Create account".

[SCREENSHOT: registration page — First name, Last name, Email, Password fields and a "Create account" button]

Fill in the form:
- First name — your first name
- Last name — your last name
- Email — your email address (used as your login)
- Password — your password

Click "Create account". After a successful registration the system will automatically redirect you to the organization creation screen.

Note: email addresses are stored in lowercase. If you enter "User@Example.com", the system will treat it as "user@example.com".


---


## Scenario 2. Logging In

Open the login page.

[SCREENSHOT: login page — Email and Password fields and a "Sign in" button]

Enter your email and password, then click "Sign in". If you already have an organization and a workspace, you will go straight to the home screen. If not, the system will prompt you to create or join an organization.


---


## Scenario 3. Creating an Organization

After registration you land on the Onboarding page. There are two options here: create a new organization, or find an existing one and submit a join request.

[SCREENSHOT: Onboarding page — "Create organization" block and "Join existing organization" block with a search field]

To create a new organization:
1. Enter the organization name in the provided field.
2. Click "Create".

You automatically become the Owner of this organization. After creation the system redirects you to the workspace selection screen.


---


## Scenario 4. Joining an Existing Organization

If your organization already exists in the system, there are two ways in: submit a join request yourself, or accept an invitation from an administrator.

### 4a. Submitting a Join Request

On the Onboarding page, type the organization name into the search field. Select the organization from the results list and click "Request to join".

[SCREENSHOT: organization search results — list with a "Request to join" button next to each result]

Your request has been sent. It will appear in the organization administrator's queue, who can approve or reject it. Once approved, you will gain access to the organization.

### 4b. Accepting an Invitation

If an administrator invited you by email, go to the "Invitations" section in the sidebar after registering.

[SCREENSHOT: Invitations page — list of invitations with "Accept" buttons]

Find the invitation from the relevant organization and click "Accept". You become a member of the organization with the role the administrator specified.


---


## Scenario 5. Creating a Workspace

While inside an organization, an administrator or owner can create a new workspace. Click the "+" button next to the "Workspaces" section in the sidebar, or go to the "Workspaces" section and click "New workspace".

[SCREENSHOT: workspace creation modal — Name field, Organization field, and Cancel and Create buttons]

Enter the workspace name. The organization is filled in automatically (the current one). Click "Create". You automatically become the Admin of this workspace.


---


## Scenario 6. Selecting a Workspace

After logging in or creating a workspace, the system shows the workspace selection screen if you have access to more than one.

[SCREENSHOT: Workspace Selector page — workspace cards with a name and an "Enter" button on each]

Click a workspace to select it. The active workspace is displayed in the header and in the sidebar. You can switch between workspaces at any time through the sidebar.


---


## Scenario 7. Working with Entities

Entities are the main content of a workspace. Clients, deals, contracts — these are all entities.

### 7a. Viewing the Entity List

In the sidebar, expand the "Entities" section. You will see sub-items by type: Client, Deal, Contract, and so on (depending on your configuration). Click the type you need.

[SCREENSHOT: entity list — a table with data columns, a search bar at the top, and a "New" button on the right]

You see all non-archived entities of that type in the current workspace. Use the search bar to find a specific record by name, email, or a numeric value.

### 7b. Creating a New Entity

Click the "New" button in the top-right corner of the entity list.

[SCREENSHOT: entity creation form — labeled fields, required ones marked with an asterisk, Cancel and Create buttons at the bottom]

A form opens. Fill in the fields:
- Fields marked with * are required.
- For some entity types (for example, deals) the form may ask you to link it to a client — choose from the list or create a new one.

Click "Create". The new record appears in the list and its detail page opens.

### 7c. Viewing Entity Details

Click any row in the list to open its details.

[SCREENSHOT: entity detail page — header with the name, Overview tab with fields, Connections panel on the right showing linked records]

The detail page contains:
- The Overview tab with all the record's properties. If this is a deal, a "Scores" block also appears here with closure probability and churn risk values calculated by the ML model.
- The Connections panel on the right, showing all links this entity has to other entities (for example, which client a deal belongs to).

### 7d. Editing an Entity

On the detail page, click "Edit" (the pencil icon or the Edit button at the top). The fields become editable.

[SCREENSHOT: edit mode — fields are active, Save and Cancel buttons are visible]

Make your changes and click "Save". If you change your mind, click "Cancel".

Some fields may be marked as read-only — their values are set automatically by the system (for example, ML scores) and cannot be changed manually.

### 7e. Archiving an Entity

On the detail page, click "Archive" (or the trash icon). A confirmation prompt appears.

[SCREENSHOT: archive confirmation dialog with "Yes" and "No" buttons]

Confirm the action. The entity is marked as archived and disappears from the active list. Archiving cannot be undone through the interface.

### 7f. Managing Entity Connections

The "Connections" panel on the right shows all existing links. There is a tab for each relationship type.

[SCREENSHOT: Connections panel — tabs for each relationship type, list of linked records, "Link existing" (chain icon) and "Create & link" (plus icon) buttons]

To add a connection, there are two options:

"Link existing" (chain icon) — opens a search dialog among existing records. Type a name, email, or ID into the search bar. Select a record from the list and click "Link".

[SCREENSHOT: Link existing dialog — search bar at the top, results list, Link button]

"Create & link" (plus icon) — opens a creation form for a new linked record, with the connection established immediately.

To remove a connection, click the unlink icon next to the relevant record in the Connections panel (available only for optional connections).


---


## Scenario 8. Working with the Graph

The graph shows the entire organization as a network: who belongs to which workspaces, what entities exist, and how they are connected.

Go to the "Graph" section in the sidebar (in the organization section).

[SCREENSHOT: Graph page — network diagram with nodes of different colors, legend at the top, filter panel]

On the graph you will see:
- Your node (dark blue, at the center) — that is you.
- Workspace nodes (teal) — workspaces you have access to.
- Entity nodes (various colors — the color depends on the entity type).
- Deal nodes — colored by risk level: red = high chance of not closing, amber = medium, green = low, grey = score unavailable.
- Other organization member nodes (light blue).

The legend above the graph explains what each color means.

### Navigating the Graph

Mouse wheel — zoom. Click and drag — pan the canvas. Click a node — opens the detail panel on the right.

[SCREENSHOT: graph with the right detail panel open after clicking a node — type badge, label, View, Edit, Delete buttons]

In the right panel for the selected node:
- View — navigate to the record's detail page.
- Edit — edit the record (if you have permission).
- Delete — archive an entity (entities only, if you have permission).

### Filtering the Graph

The filter panel sits above the graph.

[SCREENSHOT: filter panel — High/Medium/Low risk pills, Manager dropdown, Workspace dropdown, entity type chips, "X of Y visible" counter, Reset all button]

Available filters:
- Risk (High / Medium / Low) — keeps only deals of the selected risk level on the graph. Data is fetched from the server.
- Manager — shows only the workspaces and entities linked to a specific manager.
- Workspace — narrows the graph to a single workspace.
- Entity type — multi-select; keeps only entities of the selected types.

The counter in the panel header shows how many nodes are currently visible out of the total. The "Reset all" button clears every filter at once.

If the graph is empty after filtering, the message "No nodes match the active filters" appears with a "Clear all filters" button.


---


## Scenario 9. Managing Organization Members

Go to the "Members" section in the sidebar (in the organization section).

[SCREENSHOT: Members page — member table with columns: name, email, role (badge), join date; "Invite member" button in the top right]

### 9a. Inviting a New Member

Click "Invite member". Enter the person's email and optionally select a role.

[SCREENSHOT: invite modal — Email field, Role dropdown, Cancel and Invite buttons]

Click "Invite". The system generates an invitation link (since real email delivery is not configured, the link is displayed directly on screen — copy it and share it with the person manually).

### 9b. Reviewing Join Requests

If you have the "manage_join_requests" permission, you will see a "Join Requests" tab or section on the Members page.

[SCREENSHOT: join requests list — applicant name and email, "Approve" and "Reject" buttons for each row]

Click "Approve" to accept a request or "Reject" to decline it.

### 9c. Changing a Member's Role

Click a member's name in the table. Their profile page opens.

[SCREENSHOT: member profile page — name, email, current role, role dropdown, Save button]

Select a new role from the list and click "Save".

### 9d. Removing a Member

On the member's profile page, click "Remove from organization". A confirmation prompt appears. After removal, the member loses access to the organization and all its workspaces.

Note: you cannot remove a member whose role outranks yours. You also cannot remove yourself through this interface — use Account Settings to leave the organization.


---


## Scenario 10. Managing Workspace Members

Go to the "Members" section in the sidebar (in the current workspace section, under the workspace name).

[SCREENSHOT: Workspace Members page — table with name, email, role columns; "Add member" button in the top right]

### Adding a Member to the Workspace

Click "Add member". In the dialog, select an organization member from the list and choose their role in the workspace.

[SCREENSHOT: Add member dialog — organization member dropdown, Role dropdown, Cancel and Add buttons]

Click "Add".

### Changing a Role in the Workspace

Find the person in the member table. Select a new role from the dropdown in their row, or do it through their individual member page.

### Removing a Member from the Workspace

Click the remove icon next to a member. Confirm the action. The member loses access to the workspace but remains in the organization.


---


## Scenario 11. Account Settings

Click your name in the header or go to the "Account" link in the sidebar.

[SCREENSHOT: Account Settings page — First name, Last name, Email (read-only) fields, Save and Delete account buttons]

Here you can:
- Change your first and last name — make your changes and click "Save".
- Delete your account — click "Delete account". This archives your account. The email address becomes available for a new registration afterwards.

The email address cannot be changed.


---


## Scenario 12. Organization Settings

Available to the organization Owner or Admin only. Go to "Settings" in the sidebar (in the organization section).

[SCREENSHOT: Org Settings page — General section (Description field) and Membership section (Join policy dropdown, Default role dropdown), Save button]

General section: enter or update the organization description (up to 500 characters).

Membership section:
- Join policy — "Open" means anyone can submit a join request. "Invite only" means access is by invitation only; join requests are not accepted.
- Default role — the role a new member receives when a join request is approved or an invitation is accepted without an explicitly specified role.

Click "Save" to apply changes.


---


## Scenario 13. Workspace Settings

Available to the workspace Admin. Go to "Settings" in the sidebar (in the current workspace section).

[SCREENSHOT: Workspace Settings page — General section (Description field) and Risk Scoring section (Enable toggle, High risk threshold and Medium risk threshold fields), Save button]

General section: workspace description (up to 500 characters).

Risk Scoring section:
- Enable — turns ML risk scoring on or off for deals in this workspace.
- High risk threshold — a value from 0 to 1. Deals with a closure score below this threshold are considered high risk.
- Medium risk threshold — deals with a score between the medium and high thresholds are considered medium risk. Must be lower than the High risk threshold.

Click "Save".


---


## Scenario 14. Audit Log

Available to organization Owners and Admins, and to workspace Analysts. Go to the "Audit log" section in the sidebar.

[SCREENSHOT: Audit Log page — filter bar at the top (Type dropdown, date range, Action field), table below with Date, Type, Action, Author, Target, Old value, New value columns]

The table shows all recorded actions in the system. Each row contains:
- Date — when the event occurred.
- Type — the scope of the event: Organization (org-level changes), Workspace, Entity (changes to a specific record), User (profile changes).
- Action — what exactly happened.
- Author — who performed the action.
- Target — which object was affected (a clickable link to the record, if it still exists).
- Old value / New value — what changed (expands on click).

Filters:
- Type — narrow down to a specific scope (entity, workspace, organization, user).
- Date range — "Date from" and "Date to".
- Action — type an action name to search.

Click "Apply" to apply filters or "Reset" to clear them.


---


## Error Handling

### Form Validation Errors

When you fill in a form and a field fails validation, the system highlights that field immediately and shows an explanation below it. For example: "This field is required", "Value must be a valid number", "Description must not exceed 500 characters".

[SCREENSHOT: form with a highlighted field and an error message below it (red text)]

What to do: correct the value in the highlighted field according to the hint and try saving again. The form will not submit while there are empty required fields or incorrectly filled fields.

Once you fix a field, the highlight disappears automatically.

### Permission Errors (403 Forbidden)

If a notification reading "Forbidden" or "You don't have permission" appears in the bottom-right corner of the screen, your role does not allow this action.

[SCREENSHOT: toast notification in the bottom-right corner — red background, error text]

What to do: contact your organization or workspace administrator. You cannot change your own role.

### Conflict Errors (409 Conflict)

This notification appears when you try to perform an action that contradicts the current state of the data. The most common cases:
- Registering with an email already in use — "An account with this email already exists." Try a different email or sign in to the existing account.
- Inviting someone who is already a member — "This user is already a member of the organization." No invitation is needed.
- Submitting a duplicate join request — you already have a pending request for this organization. Wait for a response.

### Not Found Errors (404)

If you followed a link and see a "Not found" page, the record may have been archived or the link is outdated. Return to the list using the sidebar.

### Server Errors (500 / 502 / 503)

If a "Server error" or "Service unavailable" notification appears, this is a temporary backend issue. Wait a few seconds and try refreshing the page (F5). If the error persists, contact your system administrator.

### Deal Score Errors

If the "Scores" block on a deal detail page shows a message instead of numeric values (for example, "No analysis row found" or "Missing deal value"), the ML model could not calculate a score due to missing required data.

[SCREENSHOT: Scores block on a deal detail page — informational message instead of numeric values, "Refresh data" button]

What to do:
- Make sure the deal has all required fields filled in (status, a linked contract or a deal value, creation date).
- Click the "Refresh data" button in the top-right corner of the Scores block to request a new calculation.
- If the data is complete and the score is still unavailable, contact your system administrator.

### Graph Is Empty or Fails to Load

If you open the Graph page and nothing appears, and an error card is shown:

[SCREENSHOT: Graph page in error state — error card with a "Try again" button]

Click "Try again". If the graph remains empty, make sure you have selected an organization and that your organization has at least one workspace with entities.

If the graph loaded but is empty after applying filters, click "Clear all filters" and check whether nodes appear.
