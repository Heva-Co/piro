# Privacy Policy — Piro SMS Notifications

Last updated: 2026-07-14

Piro is an open-source uptime-monitoring and incident-alerting platform. Anyone can download, self-host, and operate their own instance of Piro, connected to their own integrations (Twilio, Email, Google Cloud, etc.). This policy describes how the Piro **software** handles data in connection with its optional SMS notification feature, regardless of who operates a given instance.

There is no central "Piro" service that processes data on behalf of multiple operators — each deployment of Piro is independently operated, and each operator (the "Operator") controls their own database, their own Twilio credentials, and their own users. This document describes the data-handling behavior built into the software itself.

## Who this applies to

This applies to end users of a Piro instance — typically an organization's own employees or on-call engineers — who choose to enable SMS as a personal notification channel within that instance's admin panel. Piro's SMS feature is built for operational alerting, not for marketing, and the software includes no functionality to use phone numbers for marketing purposes.

## What information the software collects

For each user who opts in to SMS notifications, a Piro instance stores:

- **Phone number**: the mobile number the user enters themselves when configuring SMS as a notification channel, under Configuration → Notification Preferences.
- **Verification status**: whether that phone number has been confirmed via a one-time code sent to it.
- **Message delivery metadata**: timestamps and delivery status of SMS messages sent to a verified number (returned by the Operator's own Twilio account), used only to confirm alerts were delivered.

This data is stored in the Operator's own database (self-hosted or otherwise controlled by the Operator) — not in any database controlled by the Piro project itself.

## How this information is used

The phone number and verification status are used exclusively to:

1. Send a one-time verification code when a user adds a phone number as a notification channel.
2. Send operational alert notifications (e.g. "a monitored service is down" or "a monitored service has recovered") when that user is on-call or otherwise configured to receive them.

The software includes no marketing, advertising, or any other use of this data beyond the two purposes listed above.

## Data sharing

A Piro instance sends SMS messages through the Operator's own configured Twilio account. Phone numbers and related data are transmitted only to Twilio, solely to deliver the messages described above, and are not sold, rented, or shared with any other third party by the software. Twilio processes this data under its own privacy and security obligations as the Operator's messaging subprocessor.

Because each Operator runs their own instance with their own Twilio account, an Operator's specific data-handling practices (retention periods, access controls, etc.) are governed by that Operator's own policies in addition to this document, which describes the software's built-in behavior.

## Data retention and removal

A user may remove their phone number as a notification channel at any time from the Piro admin panel (Configuration → Notification Preferences → Delete). Once removed, the software stops sending further messages to that number and deletes the stored number.

## Opt-out

Replying **STOP** to any message from a Piro instance will opt that number out of all further SMS from that instance. Replying **HELP** will return support contact information configured by that instance's Operator.

## Contact

For questions about this document or about the Piro project: https://github.com/Heva-Co/piro. For questions about how a specific instance handles your data, contact that instance's Operator directly.
