# Terms and Conditions — Piro SMS Notifications

Last updated: 2026-07-14

## Program name

**Piro Alerting SMS**

## Program description

Piro is an open-source uptime-monitoring and incident-alerting platform. Any organization can self-host their own instance of Piro, connected to their own Twilio account. This program lets an end user of a Piro instance opt in to receive SMS text messages from that instance for two purposes:

1. **One-time verification codes**, sent when the user adds their phone number as a notification channel.
2. **Operational alert notifications**, sent when a service or check monitored by that Piro instance changes status (e.g. goes down or recovers), if the user is configured to receive such alerts (for example, as part of an on-call rotation).

This program is used for operational/transactional alerting only. It is not used for marketing or promotional messaging.

## Message frequency

Message frequency varies and is driven entirely by the monitored infrastructure's behavior — a user receives a message only when a monitored service or check changes status, or when they request a one-time verification code. There is no fixed schedule; a user may receive several messages during an incident, or none for extended periods if nothing changes.

## Message and data rates

Message and data rates may apply, depending on the recipient's mobile carrier and plan.

## Consent

A user is enrolled in this program only after they voluntarily enter their own phone number into a Piro instance's admin panel and confirm control of that number by entering a one-time verification code sent to it via SMS. No phone number is enrolled without this explicit action by the user themselves.

## Opt-out and support

Reply **STOP** at any time to any message from this program to immediately stop receiving further SMS messages. Reply **HELP** to receive support contact information.

A user may also remove their phone number as a notification channel directly from the Piro admin panel (Configuration → Notification Preferences → Delete) at any time.

## Support contact

For questions about this program: https://github.com/Heva-Co/piro (open-source project) or the support contact configured by the operator of the specific Piro instance being used.

## Changes to this program

Because Piro is self-hosted open-source software, message content, frequency, and behavior are ultimately controlled by the operator of a given instance, within the scope described above. These terms describe the SMS functionality built into the Piro software itself.
