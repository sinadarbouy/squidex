/*
 * Squidex Headless CMS
 *
 * @license
 * Copyright (c) Squidex UG (haftungsbeschränkt). All rights reserved.
 */

import { UntypedFormControl, Validators } from '@angular/forms';
import { debounceTime, map, shareReplay } from 'rxjs/operators';
import { ExtendedFormGroup, Form, hasNoValue$, Types, value$ } from '@app/framework';
import { AssignContributorDto, UserDto } from '../model';

export class AssignContributorForm extends Form<ExtendedFormGroup, AssignContributorDto> {
    public get user() {
        return this.form.controls['user'];
    }

    public hasNoUser = hasNoValue$(this.user);

    constructor() {
        super(new ExtendedFormGroup({
            user: new UntypedFormControl('',
                Validators.required,
            ),
            role: new UntypedFormControl('',
                Validators.required,
            ),
        }));
    }

    protected transformSubmit(value: any) {
        let contributorId = value.user;

        if (Types.is(contributorId, UserDto)) {
            contributorId = contributorId.id;
        }

        return new AssignContributorDto({ contributorId, role: value.role, invite: true });
    }
}

export class ImportContributorsForm extends Form<ExtendedFormGroup, ReadonlyArray<AssignContributorDto>> {
    public get import() {
        return this.form.controls['import'];
    }

    public numberOfEmails = value$(this.import).pipe(debounceTime(100), map(v => extractEmails(v).length), shareReplay(1));

    public hasNoUser = this.numberOfEmails.pipe(map(v => v === 0));

    constructor() {
        super(new ExtendedFormGroup({
            import: new UntypedFormControl('',
                Validators.required,
            ),
        }));
    }

    protected transformSubmit(value: any) {
        return extractEmails(value.import);
    }
}

function extractEmails(value: string) {
    const result: AssignContributorDto[] = [];

    if (value) {
        const added: { [email: string]: boolean } = {};

        const emails = value.match(EMAIL_REGEX);
        if (emails) {
            for (const match of emails) {
                if (!added[match]) {
                    result.push(new AssignContributorDto({ contributorId: match, role: 'Editor', invite: true }));

                    added[match] = true;
                }
            }
        }
    }

    return result;
}

// eslint-disable-next-line no-useless-escape
const EMAIL_REGEX = /(?=.{1,254}$)(?=.{1,64}@)[-!#$%&'*+\/0-9=?A-Z^_`a-z{|}~]+(\.[-!#$%&'*+\/0-9=?A-Z^_`a-z{|}~]+)*@[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*/gim;
